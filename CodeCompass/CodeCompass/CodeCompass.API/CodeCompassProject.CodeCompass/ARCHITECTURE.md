# CodeCompass - AI Engineering Copilot

## Overview

CodeCompass is an AI-powered Engineering Copilot that helps developers get grounded answers from their indexed documentation and source code. It uses Retrieval-Augmented Generation (RAG) to provide context-aware responses with citations.

---

## Architecture

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│                    API Layer                             │
│         Controllers, Middleware, DI Config               │
├─────────────────────────────────────────────────────────┤
│                Application Layer                         │
│      CQRS Handlers, DTOs, Interfaces, Commands          │
├─────────────────────────────────────────────────────────┤
│                 Domain Layer                             │
│          Entities, Value Objects, Enums                  │
├─────────────────────────────────────────────────────────┤
│            Infrastructure/Repository Layer               │
│    Service Implementations, EF Core, Configuration      │
└─────────────────────────────────────────────────────────┘
```

### Dependency Direction

```
Api → Application → Domain
Api → Repository → Application → Domain
```

- **Domain** has zero dependencies (pure entities)
- **Application** depends only on Domain (defines interfaces, not implementations)
- **Repository** implements Application interfaces, references Domain
- **Api** wires everything together via DI

---

## Project Structure

```
CodeCompassProject.CodeCompass/
├── CodeCompassProject.CodeCompass.Api/           # ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── ChatController.cs                    # POST /api/chat
│   │   ├── IngestController.cs                  # POST /api/ingest/docs, /api/ingest/code
│   │   └── HealthController.cs                  # GET /api/health
│   ├── Middleware/
│   │   ├── GlobalExceptionMiddleware.cs         # Structured error handling (ProblemDetails)
│   │   └── RequestLoggingMiddleware.cs          # Request/response timing logs
│   ├── Program.cs                               # App bootstrap + DI
│   ├── appsettings.json                         # Configuration
│   └── Properties/launchSettings.json           # Dev server config
│
├── CodeCompassProject.CodeCompass.Application/   # Use Cases
│   ├── CQRS/
│   │   ├── ICommandHandler.cs                   # Generic command handler interface
│   │   └── IQueryHandler.cs                     # Generic query handler interface
│   ├── Commands/
│   │   ├── SendChatMessageCommand.cs            # Chat command model
│   │   ├── SendChatMessageHandler.cs            # Orchestrates RAG pipeline
│   │   ├── IngestDocumentsCommand.cs            # Doc ingestion command
│   │   ├── IngestDocumentsHandler.cs            # Chunks + embeds + stores docs
│   │   ├── IngestCodeCommand.cs                 # Code ingestion command
│   │   └── IngestCodeHandler.cs                 # Chunks + embeds + stores code
│   ├── Queries/
│   │   ├── GetHealthQuery.cs                    # Health check query
│   │   └── GetHealthHandler.cs                  # Pings all services
│   ├── DTOs/
│   │   ├── ChatRequest.cs                       # Input: question + optional session
│   │   ├── ChatResponse.cs                      # Output: answer + citations + session
│   │   ├── IngestDocsRequest.cs                 # Input: files + metadata
│   │   ├── IngestCodeRequest.cs                 # Input: files + repo name
│   │   ├── IngestResponse.cs                    # Output: chunks/sources processed
│   │   └── HealthResponse.cs                    # Output: service health statuses
│   ├── Interfaces/
│   │   ├── IVectorStore.cs                      # Store/search embeddings
│   │   ├── IEmbeddingService.cs                 # Generate text embeddings
│   │   ├── ILLMService.cs                       # Chat completion
│   │   └── IDocumentIngestionService.cs         # Chunk documents/code
│   └── Models/
│       └── ChatCompletionResult.cs              # LLM response model
│
├── CodeCompassProject.CodeCompass.Domain/        # Core Business Logic
│   ├── Entities/
│   │   ├── DocumentChunk.cs                     # Indexed content chunk
│   │   ├── ChatSession.cs                       # Conversation session
│   │   └── ChatMessage.cs                       # Individual message
│   ├── ValueObjects/
│   │   └── Citation.cs                          # Source reference
│   └── Enums/
│       ├── SourceType.cs                        # Documentation | Code
│       └── MessageRole.cs                       # User | Assistant
│
├── CodeCompassProject.CodeCompass.Repository/    # Infrastructure
│   ├── Services/
│   │   ├── InMemoryVectorStore.cs               # Dev vector store (cosine similarity)
│   │   ├── AzureOpenAILLMService.cs             # LLM service (placeholder)
│   │   ├── AzureOpenAIEmbeddingService.cs       # Embedding service (placeholder)
│   │   └── DefaultDocumentIngestionService.cs   # Text chunking with overlap
│   ├── Configuration/
│   │   ├── AzureOpenAISettings.cs               # Azure OpenAI config model
│   │   ├── VectorStoreSettings.cs               # Vector store config model
│   │   └── IngestionSettings.cs                 # Chunking config model
│   ├── CodeCompassDbContext.cs                  # EF Core context (future use)
│   └── DependencyInjection.cs                   # Service registration extension
│
└── CodeCompassProject.CodeCompass.Test/          # Unit/Integration Tests
```

---

## Design Patterns

### CQRS (Command Query Responsibility Segregation)

Commands (write operations) and queries (read operations) are separated:

```csharp
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}
```

No MediatR dependency — handlers are registered directly in DI.

### Dependency Injection

All services are registered in `DependencyInjection.cs`:
- `IVectorStore` → `InMemoryVectorStore` (Singleton — shared state)
- `IEmbeddingService` → `AzureOpenAIEmbeddingService` (Scoped)
- `ILLMService` → `AzureOpenAILLMService` (Scoped)
- `IDocumentIngestionService` → `DefaultDocumentIngestionService` (Scoped)
- All CQRS handlers registered as Scoped

### RAG Pipeline (Chat Flow)

```
User Question
    │
    ▼
┌──────────────────┐
│ Embed Question   │  ← IEmbeddingService
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ Vector Search    │  ← IVectorStore.SearchAsync (top-K)
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ Build Context    │  ← Concatenate relevant chunks
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ LLM Completion   │  ← ILLMService (system prompt + context + question)
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ Return Answer    │  ← Answer + Citations from retrieved chunks
└──────────────────┘
```

### Ingestion Pipeline

```
Upload Files
    │
    ▼
┌──────────────────┐
│ Chunk Content    │  ← IDocumentIngestionService (size + overlap)
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ Embed Chunks     │  ← IEmbeddingService (batch)
└──────────────────┘
    │
    ▼
┌──────────────────┐
│ Store Vectors    │  ← IVectorStore.StoreAsync
└──────────────────┘
```

---

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/chat` | Send a question, get a grounded answer with citations |
| POST | `/api/ingest/docs` | Upload documentation files for indexing |
| POST | `/api/ingest/code` | Upload source code files for indexing |
| GET | `/api/health` | Health check for all services |

### Request/Response Examples

#### POST /api/chat
```json
// Request
{
  "question": "How does the authentication middleware work?",
  "sessionId": "optional-guid-for-conversation-continuity"
}

// Response
{
  "answer": "Based on the indexed documentation...",
  "citations": [
    {
      "sourceUri": "auth-middleware.md",
      "chunkContent": "The authentication middleware validates JWT tokens...",
      "relevanceScore": 0.95
    }
  ],
  "sessionId": "guid"
}
```

#### POST /api/ingest/docs
```
Content-Type: multipart/form-data
Files: [file1.md, file2.pdf, ...]
```
```json
// Response
{
  "chunksIngested": 42,
  "sourcesProcessed": 3
}
```

#### GET /api/health
```json
{
  "status": "Healthy",
  "services": [
    { "name": "VectorStore", "status": "Healthy", "responseTimeMs": 2 },
    { "name": "EmbeddingService", "status": "Healthy", "responseTimeMs": 150 },
    { "name": "LLMService", "status": "Healthy", "responseTimeMs": 300 }
  ]
}
```

---

## Configuration

### appsettings.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  },
  "VectorStore": {
    "Type": "InMemory",
    "ConnectionString": ""
  },
  "Ingestion": {
    "MaxChunkSize": 512,
    "ChunkOverlap": 50
  }
}
```

---

## Domain Models

### DocumentChunk
| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Unique identifier |
| Content | string | The text content of the chunk |
| SourceUri | string | Original file/URL source |
| SourceType | SourceType | Documentation or Code |
| Metadata | Dictionary<string, string> | Flexible key-value metadata |
| EmbeddingVector | float[] | Vector embedding for similarity search |
| CreatedAt | DateTime | Timestamp |

### ChatSession
| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Session identifier |
| CreatedAt | DateTime | When session started |
| Messages | List<ChatMessage> | Conversation history |

### ChatMessage
| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Message identifier |
| SessionId | Guid | Parent session |
| Role | MessageRole | User or Assistant |
| Content | string | Message text |
| Citations | List<Citation> | Sources referenced |
| Timestamp | DateTime | When sent |

### Citation
| Property | Type | Description |
|----------|------|-------------|
| SourceUri | string | Document/file source |
| ChunkContent | string | Relevant excerpt |
| RelevanceScore | double | Similarity score |

---

## Error Handling

### GlobalExceptionMiddleware

All unhandled exceptions are caught and returned as RFC 7807 ProblemDetails:

```json
{
  "status": 500,
  "title": "An internal error occurred",
  "detail": "An unexpected error occurred. Please try again later.",
  "instance": "/api/chat"
}
```

Exception-to-status mapping:
- `ArgumentException` → 400
- `InvalidOperationException` → 400
- `UnauthorizedAccessException` → 401
- `FileNotFoundException` → 404
- `OperationCanceledException` → 400
- Everything else → 500 (details hidden from client)

### Request Logging

Every request is logged with:
- Unique request ID
- HTTP method and path
- Response status code
- Elapsed time in milliseconds

---

## Current State (Placeholder Implementations)

The following services currently use **placeholder implementations** that allow the project to run without external dependencies:

| Service | Current Implementation | Behavior |
|---------|----------------------|----------|
| IVectorStore | InMemoryVectorStore | Cosine similarity search in memory |
| IEmbeddingService | AzureOpenAIEmbeddingService | Deterministic pseudo-embeddings from text hash |
| ILLMService | AzureOpenAILLMService | Returns formatted placeholder response |

---

## Future Improvements

### 1. Real Azure OpenAI Integration

Replace placeholder implementations with actual Azure.AI.OpenAI SDK calls:

```bash
dotnet add package Azure.AI.OpenAI --version 2.0.0
```

Wire up `OpenAIClient` in `AzureOpenAILLMService` and `AzureOpenAIEmbeddingService`.

### 2. Vector Database Integration

#### Option A: Azure AI Search (Recommended for Azure ecosystem)

```bash
dotnet add package Azure.Search.Documents
```

Implementation approach:
1. Create `AzureAISearchVectorStore : IVectorStore`
2. Use `SearchClient` for vector search with hybrid retrieval
3. Create an index with vector field (dimensions: 1536 for ada-002)
4. Store metadata as filterable fields
5. Add `VectorStoreSettings.Type = "AzureAISearch"` with connection string + index name
6. Register conditionally in DI based on config

#### Option B: Qdrant (Open-source, self-hosted)

```bash
dotnet add package Qdrant.Client
```

Implementation approach:
1. Create `QdrantVectorStore : IVectorStore`
2. Use `QdrantClient` for upsert/search operations
3. Create collection with vector config (size: 1536, distance: Cosine)
4. Map `DocumentChunk.Metadata` to Qdrant payload

#### Option C: Pinecone (Managed cloud)

```bash
dotnet add package Pinecone.NET
```

#### Conditional Registration Pattern

```csharp
var vectorStoreType = configuration["VectorStore:Type"];
services.AddSingleton<IVectorStore>(vectorStoreType switch
{
    "AzureAISearch" => sp => new AzureAISearchVectorStore(...),
    "Qdrant" => sp => new QdrantVectorStore(...),
    _ => sp => new InMemoryVectorStore(...)
});
```

### 3. Document Parsing Improvements

- **PDF parsing**: Add `PdfPig` or `iText7` for PDF text extraction
- **Markdown AST**: Use `Markdig` to parse markdown with structure awareness
- **Code parsing**: Use Roslyn for C# AST-based chunking, Tree-sitter for other languages
- **Recursive chunking**: Split by headings → paragraphs → sentences → words

### 4. Session Persistence

The `CodeCompassDbContext` with EF Core is already set up. To persist chat history:
1. Add a `ChatSessionRepository` implementing a new `IChatSessionRepository` interface
2. Store sessions and messages in SQL Server
3. Load conversation history for multi-turn context in the LLM prompt

### 5. Authentication & Authorization

- Add JWT bearer authentication
- Implement API key middleware for service-to-service calls
- Add rate limiting per user/API key

### 6. Observability

- **Structured logging**: Add Serilog with sinks (Seq, Application Insights, ELK)
- **Distributed tracing**: Add OpenTelemetry instrumentation
- **Metrics**: Track token usage, latency percentiles, ingestion throughput
- **Health checks**: Use ASP.NET Core `IHealthCheck` for deep service probes

### 7. Caching

- Cache frequent embeddings (same questions get same vectors)
- Cache LLM responses for identical question + context combinations
- Use `IDistributedCache` with Redis for multi-instance deployments

### 8. Async Ingestion

- Move ingestion to a background job queue (Hangfire, Azure Queue + Worker)
- Return 202 Accepted with a job ID
- Add a `GET /api/ingest/status/{jobId}` endpoint

### 9. Streaming Responses

- Add SSE (Server-Sent Events) endpoint for streaming LLM responses
- `POST /api/chat/stream` → returns token-by-token as they arrive

### 10. Multi-tenancy

- Add tenant isolation at the vector store level
- Namespace collections/indexes per tenant
- Filter searches by tenant ID

---

## How to Run

```bash
# Build
dotnet build

# Run (from Api project directory)
dotnet run --project CodeCompassProject.CodeCompass.Api

# Access Swagger
# https://localhost:7133/swagger
# http://localhost:5025/swagger
```

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | ASP.NET Core 9.0 |
| Language | C# 13 |
| Architecture | Clean Architecture |
| Pattern | CQRS (no MediatR) |
| ORM | Entity Framework Core 9.0 |
| API Docs | Swashbuckle (Swagger/OpenAPI) |
| Vector Store | In-Memory (swappable) |
| LLM | Azure OpenAI (placeholder) |
| Embeddings | Azure OpenAI ada-002 (placeholder) |
| Logging | Microsoft.Extensions.Logging |
| Error Handling | ProblemDetails (RFC 7807) |

---

## Requirements Checklist

- [x] ASP.NET Core Web API
- [x] Clean Architecture (4 layers, proper dependency direction)
- [x] CQRS (commands for chat/ingest, queries for health)
- [x] Dependency Injection (all services interface-based)
- [x] Swagger (OpenAPI with XML docs)
- [x] Logging (structured, per-request)
- [x] Error handling middleware (GlobalExceptionMiddleware)
- [x] POST /api/chat (question → grounded answer with citations)
- [x] POST /api/ingest/docs (document ingestion)
- [x] POST /api/ingest/code (code ingestion)
- [x] GET /api/health (service health check)
- [x] IVectorStore interface + InMemory implementation
- [x] IEmbeddingService interface + placeholder implementation
- [x] ILLMService interface + placeholder implementation
- [x] IDocumentIngestionService interface + chunking implementation
- [x] Production-ready patterns (middleware, config, DI, error handling)
