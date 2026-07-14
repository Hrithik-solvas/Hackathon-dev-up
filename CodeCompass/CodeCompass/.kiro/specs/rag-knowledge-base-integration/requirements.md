# Requirements Document

## Introduction

This feature integrates the CodeCompass.API project with the CodeCompass.RAG library to replace placeholder implementations with real RAG-powered search and answer generation. The system uses two distinct knowledge bases — one for product/domain knowledge (CLO/CDO portfolio management) and one for modern platform technical architecture — and intelligently routes user questions to the appropriate knowledge base based on question classification.

## Glossary

- **CodeCompass_API**: The ASP.NET Core 9 Web API that exposes chat and ingestion endpoints, structured using Clean Architecture and CQRS patterns.
- **CodeCompass_RAG**: The modular pipeline library providing vector search, embedding generation, document parsing, chunking, and indexing capabilities backed by Azure AI Search and Azure OpenAI.
- **Question_Router**: A component that classifies incoming user questions and determines which knowledge base to query.
- **Knowledge_Base**: A logically partitioned set of indexed documents within the Azure AI Search index, distinguished by source path prefix.
- **Product_Knowledge_Base**: The knowledge base containing CLO/CDO portfolio management, compliance, trading, waterfall, and reporting domain content (Solvas_AM_Classic_Product_Knowledge_Base).
- **Tech_Stack_Knowledge_Base**: The knowledge base containing React micro-frontends, .NET microservices, gRPC, Kubernetes, and CI/CD architecture content (Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base).
- **SearchFilter**: A CodeCompass.RAG model that enables filtering vector search results by ContentType, Language, or SourcePathPrefix.
- **SendChatMessageHandler**: The CQRS command handler in CodeCompass_API that orchestrates the RAG pipeline (embed → search → build context → LLM → respond).
- **IVectorSearch**: The CodeCompass.RAG interface that executes semantic similarity queries against the Azure AI Search vector index.
- **IEmbeddingGenerator**: The CodeCompass.RAG interface that generates vector embeddings via Azure OpenAI.
- **IPipelineOrchestrator**: The CodeCompass.RAG interface that coordinates the full indexing pipeline for ingesting documents into Azure AI Search.
- **Citation**: A reference to the source document chunk used to ground an answer.

## Requirements

### Requirement 1: Replace Placeholder Embedding Service with RAG Embedding Generator

**User Story:** As a developer, I want the API to use real Azure OpenAI embeddings from CodeCompass.RAG, so that user questions are converted into meaningful vector representations for semantic search.

#### Acceptance Criteria

1. WHEN the CodeCompass_API starts, THE DI_Container SHALL register an adapter that delegates to the CodeCompass.RAG IEmbeddingGenerator as the implementation of the API IEmbeddingService interface, replacing the placeholder AzureOpenAIEmbeddingService.
2. WHEN a user submits a question via POST /api/chat, THE SendChatMessageHandler SHALL invoke the IEmbeddingGenerator.GenerateEmbeddingAsync method with the question text and produce a non-empty float array embedding for use in vector search.
3. IF the IEmbeddingGenerator throws an exception during embedding generation, THEN THE SendChatMessageHandler SHALL return an error response with HTTP status code 502 and an error message indicating that embedding generation failed.
4. IF the IEmbeddingGenerator returns an empty vector (zero-length array) without throwing an exception, THEN THE SendChatMessageHandler SHALL return an error response with HTTP status code 502 and an error message indicating that a valid embedding could not be produced.
5. IF the question text in the chat request is null or empty, THEN THE SendChatMessageHandler SHALL return an error response with HTTP status code 400 and an error message indicating that a non-empty question is required.

### Requirement 2: Replace Placeholder Vector Store with RAG Vector Search

**User Story:** As a developer, I want the API to use CodeCompass.RAG's Azure AI Search vector search instead of the in-memory vector store, so that search results come from the production index containing ingested knowledge base content.

#### Acceptance Criteria

1. WHEN the CodeCompass_API starts, THE DI_Container SHALL register the CodeCompass.RAG IVectorSearch as the implementation for vector search operations in the SendChatMessageHandler, replacing the IVectorStore dependency.
2. WHEN the SendChatMessageHandler processes a user question, THE SendChatMessageHandler SHALL call IVectorSearch.SearchAsync with a SearchRequest containing the user question as the Query and a TopK value of 5, without first generating an embedding (since IVectorSearch handles embedding internally).
3. THE SendChatMessageHandler SHALL map each SearchHit.ChunkText from the SearchResult.Hits collection into the context string list used for LLM prompt construction.
4. THE SendChatMessageHandler SHALL map each SearchHit into a CitationDto where SourceUri is set from SearchHit.Metadata.SourceFilePath, ChunkContent is set from SearchHit.ChunkText truncated to 200 characters with a "..." suffix if it exceeds 200 characters, and RelevanceScore is set from SearchHit.RelevanceScore.
5. IF IVectorSearch.SearchAsync returns a SearchResult with an empty Hits collection, THEN THE SendChatMessageHandler SHALL proceed to call the LLM with an empty context list and return a ChatResponse with an empty Citations list.

### Requirement 3: Question Classification and Routing

**User Story:** As a user, I want my questions automatically routed to the correct knowledge base, so that I receive answers grounded in the most relevant domain content without manually specifying a source.

#### Acceptance Criteria

1. WHEN a user submits a question, THE Question_Router SHALL classify the question into one of: "product", "tech_stack", or "both".
2. WHEN the Question_Router classifies a question as "product", THE SendChatMessageHandler SHALL query only the Product_Knowledge_Base by setting SearchFilter.SourcePathPrefix to the product knowledge base path.
3. WHEN the Question_Router classifies a question as "tech_stack", THE SendChatMessageHandler SHALL query only the Tech_Stack_Knowledge_Base by setting SearchFilter.SourcePathPrefix to the tech stack knowledge base path.
4. WHEN the Question_Router classifies a question as "both", THE SendChatMessageHandler SHALL query both knowledge bases, merge the results, order them by RelevanceScore descending, and return only the top-K results where K equals the same top-K value used for single knowledge base queries.
5. THE Question_Router SHALL classify questions by performing case-insensitive matching against domain keyword sets, where the product keyword set includes: CLO, CDO, compliance, waterfall, trading, portfolio management, tranches, overcollateralization, and reporting, and the tech_stack keyword set includes: React, micro-frontends, .NET, microservices, gRPC, Kubernetes, CI/CD, Docker, and deployment pipelines.
6. IF the question matches keywords from both the product and tech_stack keyword sets, THEN THE Question_Router SHALL classify the question as "both".
7. IF the question does not match any keyword from either keyword set, THEN THE Question_Router SHALL default to the classification "both".
8. IF the user submits an empty or whitespace-only question, THEN THE SendChatMessageHandler SHALL return an error response indicating that the question must not be empty.
9. THE Question_Router SHALL complete classification within 100 milliseconds per question.

### Requirement 4: Knowledge Base Ingestion via RAG Pipeline

**User Story:** As an administrator, I want to ingest the two knowledge base documents into the Azure AI Search index via the RAG pipeline, so that the vector store contains searchable content for both domains.

#### Acceptance Criteria

1. WHEN an administrator triggers ingestion for the Product_Knowledge_Base, THE IPipelineOrchestrator SHALL execute RunAsync with a PipelineRequest where TargetPath points to the directory containing Solvas_AM_Classic_Product_Knowledge_Base.md and Mode is set to Full, indexing the file's chunks into the Azure AI Search index.
2. WHEN an administrator triggers ingestion for the Tech_Stack_Knowledge_Base, THE IPipelineOrchestrator SHALL execute RunAsync with a PipelineRequest where TargetPath points to the directory containing Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base.md and Mode is set to Full, indexing the file's chunks into the Azure AI Search index.
3. THE IPipelineOrchestrator SHALL preserve the source file path in ChunkMetadata.SourceFilePath for each indexed chunk so that SearchFilter.SourcePathPrefix routing functions correctly.
4. WHEN ingestion completes successfully, THE IPipelineOrchestrator SHALL return a PipelineResult with TotalFilesProcessed greater than or equal to 1, TotalChunksGenerated greater than or equal to 1, and TotalErrors equal to 0.
5. IF the directory specified in PipelineRequest.TargetPath does not exist, THEN THE IPipelineOrchestrator SHALL throw a DirectoryNotFoundException with a message that includes the invalid path value.
6. IF ingestion completes with TotalErrors greater than 0, THEN THE IPipelineOrchestrator SHALL still return a PipelineResult containing the error count and partial processing totals rather than throwing an exception.

### Requirement 5: RAG Service Registration and Configuration

**User Story:** As a developer, I want a single DI registration call to wire up all CodeCompass.RAG services in the API, so that configuration is centralized and maintainable.

#### Acceptance Criteria

1. THE CodeCompass_API SHALL call services.AddCodeCompassPipeline(configuration) exactly once during startup to register all RAG pipeline services including parsers, chunking, embedding, vector store, vector search, indexing, and pipeline orchestrator.
2. THE CodeCompass_API appsettings.json SHALL contain an "AzureOpenAI" configuration section with Endpoint (non-empty string), ApiKey (non-empty string), DeploymentName (non-empty string), and EmbeddingDeploymentName (non-empty string) values.
3. THE CodeCompass_API appsettings.json SHALL contain an "AzureSearch" configuration section with Endpoint (non-empty string), ApiKey (non-empty string), and IndexName (non-empty string) values.
4. THE CodeCompass_API appsettings.json SHALL contain a "KnowledgeBases" configuration section mapping logical names ("Product", "TechStack") to their respective SourcePathPrefix values, where each SourcePathPrefix is a non-empty string.
5. IF any of the following required configuration values are missing or empty at startup — AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, AzureOpenAI:DeploymentName, AzureOpenAI:EmbeddingDeploymentName, AzureSearch:Endpoint, AzureSearch:ApiKey, AzureSearch:IndexName, KnowledgeBases:Product:SourcePathPrefix, or KnowledgeBases:TechStack:SourcePathPrefix — THEN THE CodeCompass_API SHALL fail to start and throw an exception with an error message indicating which configuration value is missing or empty.
6. WHEN services.AddCodeCompassPipeline(configuration) completes successfully, THE CodeCompass_API DI container SHALL resolve IPipelineOrchestrator, IVectorSearch, IEmbeddingGenerator, and IChunkingService without throwing an exception.
7. IF the AddCodeCompassPipeline registration is called more than once, THEN THE CodeCompass_API SHALL not register duplicate service instances.

### Requirement 6: LLM Integration for Answer Generation

**User Story:** As a user, I want the system to generate natural language answers grounded in the retrieved context, so that I receive helpful, cited responses rather than raw document chunks.

#### Acceptance Criteria

1. THE SendChatMessageHandler SHALL construct a system prompt instructing the LLM to answer only based on the provided context chunks and to cite the originating knowledge base name (Product_Knowledge_Base or Tech_Stack_Knowledge_Base) for each piece of information referenced in the answer.
2. WHEN context chunks are retrieved from vector search, THE SendChatMessageHandler SHALL include the knowledge base origin identifier with each context chunk and pass the annotated context along with the user question to the ILLMService.GetCompletionAsync method.
3. THE ILLMService SHALL call Azure OpenAI chat completion using the configured deployment model with a request timeout of 30 seconds.
4. IF the ILLMService throws an exception during completion, THEN THE SendChatMessageHandler SHALL return an HTTP 503 error response with a message indicating the LLM completion service is temporarily unavailable.
5. IF the ILLMService returns a ChatCompletionResult with empty Content, THEN THE SendChatMessageHandler SHALL return a ChatResponse with an Answer indicating the system was unable to generate a response from the provided context.

### Requirement 7: Citation Accuracy in Responses

**User Story:** As a user, I want each answer to include accurate citations referencing the source knowledge base and relevant section, so that I can verify the information.

#### Acceptance Criteria

1. THE ChatResponse.Citations SHALL contain exactly one Citation for each SearchHit used to construct the LLM context, ordered by RelevanceScore descending.
2. THE Citation.SourceUri SHALL contain the SourceFilePath from the SearchHit.Metadata identifying which knowledge base the chunk originated from.
3. THE Citation.RelevanceScore SHALL contain the RelevanceScore from the SearchHit as a floating-point value between 0.0 and 1.0 inclusive.
4. IF the SearchHit.ChunkText length exceeds 200 characters, THEN THE Citation.ChunkContent SHALL contain the first 200 characters of the SearchHit.ChunkText followed by an ellipsis ("...").
5. IF the SearchHit.ChunkText length is 200 characters or fewer, THEN THE Citation.ChunkContent SHALL contain the complete SearchHit.ChunkText without modification.
6. IF a SearchHit has empty or null ChunkText, THEN THE Citation.ChunkContent SHALL contain an empty string.

### Requirement 8: Graceful Degradation and Error Handling

**User Story:** As a user, I want the system to handle failures gracefully, so that I receive informative error messages instead of cryptic failures when a service is unavailable.

#### Acceptance Criteria

1. IF the Azure AI Search service is unreachable during vector search after retry attempts are exhausted, THEN THE SendChatMessageHandler SHALL propagate an exception that the GlobalExceptionMiddleware converts into an RFC 7807 ProblemDetails response with HTTP status 503 and a detail message indicating the search service is temporarily unavailable.
2. IF the Azure OpenAI embedding service is unreachable after retry attempts are exhausted, THEN THE SendChatMessageHandler SHALL propagate an exception that the GlobalExceptionMiddleware converts into an RFC 7807 ProblemDetails response with HTTP status 503 and a detail message indicating the embedding service is temporarily unavailable.
3. IF the Azure OpenAI LLM service is unreachable after retry attempts are exhausted, THEN THE SendChatMessageHandler SHALL propagate an exception that the GlobalExceptionMiddleware converts into an RFC 7807 ProblemDetails response with HTTP status 503 and a detail message indicating the completion service is temporarily unavailable.
4. IF the vector search returns zero results for a question, THEN THE SendChatMessageHandler SHALL return a ChatResponse with an Answer indicating no relevant information was found, an empty Citations list, and a valid SessionId.
5. WHILE the CodeCompass_API is processing a chat request, THE RequestLoggingMiddleware SHALL log the HTTP method, request path, response status code, and total elapsed time in milliseconds for each request.
