# Implementation Plan: RAG Knowledge Base Integration

## Overview

This plan implements the integration of CodeCompass.API with the CodeCompass.RAG library, replacing placeholder implementations with production-grade RAG-powered search and answer generation. The implementation follows Clean Architecture principles, uses the Adapter Pattern for bridging interfaces, and introduces keyword-based question routing across two knowledge bases.

## Tasks

- [x] 1. Set up configuration models and appsettings structure
  - [x] 1.1 Create KnowledgeBasesSettings configuration model
    - Create `KnowledgeBasesSettings` class with `SectionName` constant, `Product` and `TechStack` properties of type `KnowledgeBaseEntry`
    - Create `KnowledgeBaseEntry` class with `SourcePathPrefix` property
    - Place in the Application or Domain layer configuration models folder
    - _Requirements: 5.4_

  - [x] 1.2 Update appsettings.json with required configuration sections
    - Add `AzureOpenAI` section with `Endpoint`, `ApiKey`, `DeploymentName`, `EmbeddingDeploymentName` keys
    - Add `AzureSearch` section with `Endpoint`, `ApiKey`, `IndexName` keys
    - Add `KnowledgeBases` section with `Product.SourcePathPrefix` and `TechStack.SourcePathPrefix` values
    - Use placeholder values for secrets (to be replaced by environment/secrets management)
    - _Requirements: 5.2, 5.3, 5.4_

  - [x] 1.3 Create ConfigurationValidationHostedService
    - Implement `IHostedService` with `StartAsync` that validates all required configuration values are present and non-empty
    - Validate: `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`, `AzureOpenAI:EmbeddingDeploymentName`, `AzureSearch:Endpoint`, `AzureSearch:ApiKey`, `AzureSearch:IndexName`, `KnowledgeBases:Product:SourcePathPrefix`, `KnowledgeBases:TechStack:SourcePathPrefix`
    - Throw an exception with a descriptive message indicating which value is missing or empty
    - _Requirements: 5.5_

- [x] 2. Implement Question Router
  - [x] 2.1 Create IQuestionRouter interface and QuestionClassification enum
    - Define `IQuestionRouter` interface with `Classify(string question)` method in the Application Interfaces folder
    - Define `QuestionClassification` enum with values: `Product`, `TechStack`, `Both`
    - _Requirements: 3.1_

  - [x] 2.2 Implement KeywordQuestionRouter
    - Create `KeywordQuestionRouter` implementing `IQuestionRouter` in the Repository/Services layer
    - Define static `ProductKeywords` HashSet with: CLO, CDO, compliance, waterfall, trading, portfolio management, tranches, overcollateralization, reporting
    - Define static `TechStackKeywords` HashSet with: React, micro-frontends, .NET, microservices, gRPC, Kubernetes, CI/CD, Docker, deployment pipelines
    - Use `StringComparer.OrdinalIgnoreCase` for the HashSets
    - Implement `Classify` using `string.Contains` with `StringComparison.OrdinalIgnoreCase` for keyword matching
    - Return `Both` for null/whitespace input or when no keywords match or both keyword sets match
    - _Requirements: 3.1, 3.5, 3.6, 3.7, 3.9_

  - [ ]* 2.3 Write property test for question classification correctness
    - **Property 2: Question classification correctness**
    - Test that for any generated string, classification follows keyword presence rules: product-only keywords → Product, tech-stack-only keywords → TechStack, both or neither → Both
    - Verify case-insensitivity by generating mixed-case variants
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6, 3.7**

  - [ ]* 2.4 Write property test for question classification performance
    - **Property 3: Question classification performance**
    - Test that for any question string of any length, `Classify` completes within 100 milliseconds
    - Use FsCheck.Xunit with strings of varying lengths including very long strings
    - **Validates: Requirements 3.9**

- [x] 3. Implement EmbeddingServiceAdapter
  - [x] 3.1 Create EmbeddingServiceAdapter class
    - Implement `IEmbeddingService` in the Repository/Services layer
    - Inject `IEmbeddingGenerator` via constructor
    - Delegate `GetEmbeddingAsync` to `IEmbeddingGenerator.GenerateEmbeddingAsync`
    - Delegate `GetEmbeddingsAsync` to `IEmbeddingGenerator.GenerateEmbeddingsBatchAsync`
    - _Requirements: 1.1, 1.2_

  - [ ]* 3.2 Write unit tests for EmbeddingServiceAdapter
    - Test that `GetEmbeddingAsync` delegates correctly to `IEmbeddingGenerator.GenerateEmbeddingAsync`
    - Test that `GetEmbeddingsAsync` delegates correctly to `IEmbeddingGenerator.GenerateEmbeddingsBatchAsync`
    - Test that exceptions from `IEmbeddingGenerator` propagate through the adapter
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Modify SendChatMessageHandler for RAG integration
  - [x] 5.1 Update SendChatMessageHandler dependencies and constructor
    - Replace `IVectorStore` dependency with `IVectorSearch` (from CodeCompass.RAG)
    - Remove `IEmbeddingService` dependency (IVectorSearch handles embedding internally)
    - Add `IQuestionRouter` dependency
    - Add `IOptions<KnowledgeBasesSettings>` dependency
    - Update constructor to inject all new dependencies
    - _Requirements: 2.1, 3.1_

  - [x] 5.2 Implement question validation in SendChatMessageHandler
    - Add validation at the start of `Handle` method to check if question is null, empty, or whitespace
    - Return 400 error response with message "A non-empty question is required" if validation fails
    - Ensure IVectorSearch and ILLMService are NOT invoked when question is invalid
    - _Requirements: 1.5, 3.8_

  - [x] 5.3 Implement question routing and vector search invocation
    - Call `IQuestionRouter.Classify(question)` to get classification
    - For `Product` classification: call `IVectorSearch.SearchAsync` with `SearchFilter.SourcePathPrefix` set to Product knowledge base path from settings
    - For `TechStack` classification: call `IVectorSearch.SearchAsync` with `SearchFilter.SourcePathPrefix` set to TechStack knowledge base path from settings
    - For `Both` classification: call `IVectorSearch.SearchAsync` for both knowledge bases, merge results, order by `RelevanceScore` descending, take top-K (5) results
    - Set `TopK` to 5 in the `SearchRequest`
    - _Requirements: 2.2, 3.2, 3.3, 3.4_

  - [x] 5.4 Implement context construction and LLM invocation
    - Map each `SearchHit.ChunkText` into a context string annotated with knowledge base origin (derived from `SourceFilePath` matching against `KnowledgeBasesSettings` path prefixes)
    - Construct system prompt instructing LLM to answer only from context and cite knowledge base names
    - Pass annotated context and user question to `ILLMService.GetCompletionAsync`
    - Handle empty LLM content: return `ChatResponse` with fallback answer message
    - Handle zero search results: call LLM with empty context, return `ChatResponse` with empty citations
    - _Requirements: 2.3, 2.5, 6.1, 6.2, 6.5, 8.4_

  - [x] 5.5 Implement citation mapping in SendChatMessageHandler
    - Map each `SearchHit` to a `CitationDto` where:
      - `SourceUri` = `SearchHit.Metadata.SourceFilePath`
      - `RelevanceScore` = `SearchHit.RelevanceScore` (float in [0.0, 1.0])
      - `ChunkContent` = `SearchHit.ChunkText` truncated to 200 chars + "..." if exceeding 200, full text if ≤ 200, empty string if null/empty
    - Order citations by `RelevanceScore` descending
    - Return exactly one citation per search hit used
    - _Requirements: 2.4, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [ ]* 5.6 Write property test for empty/whitespace question rejection
    - **Property 1: Empty/whitespace question rejection**
    - Generate null, empty, and whitespace-only strings; verify handler returns 400 and does NOT invoke IVectorSearch or ILLMService
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 1.5, 3.8**

  - [ ]* 5.7 Write property test for "Both" classification merge, sort, and cap
    - **Property 4: "Both" classification merge, sort, and cap**
    - Generate two collections of SearchHits with random RelevanceScores; verify merged result is sorted descending and capped at K=5
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 3.4**

  - [ ]* 5.8 Write property test for context construction from search hits
    - **Property 5: Context construction from search hits**
    - Generate non-empty SearchResult with varying hits; verify context list has exactly one entry per hit, each containing ChunkText annotated with KB origin
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.3, 6.2**

  - [ ]* 5.9 Write property test for citation field mapping correctness
    - **Property 6: Citation field mapping correctness**
    - Generate SearchHits with varying ChunkText lengths (including null, empty, exactly 200, and >200 chars); verify CitationDto mapping rules
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.4, 7.2, 7.3, 7.4, 7.5, 7.6**

  - [ ]* 5.10 Write property test for citation count and ordering invariant
    - **Property 7: Citation count and ordering invariant**
    - Generate SearchResults with N hits (0 ≤ N ≤ 20); verify Citations list has exactly N elements ordered by RelevanceScore descending
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 7.1**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Update DI Registration and remove placeholder implementations
  - [x] 7.1 Update DependencyInjection.cs with RAG service registration
    - Call `services.AddCodeCompassPipeline(configuration)` to register all RAG services
    - Register `services.Configure<KnowledgeBasesSettings>(configuration.GetSection("KnowledgeBases"))`
    - Register `IEmbeddingService` as `EmbeddingServiceAdapter` (scoped)
    - Register `IQuestionRouter` as `KeywordQuestionRouter` (singleton)
    - Register `ConfigurationValidationHostedService` as hosted service
    - Remove `InMemoryVectorStore` registration
    - Remove placeholder `AzureOpenAIEmbeddingService` registration
    - Update `SendChatMessageHandler` registration to reflect new dependencies
    - _Requirements: 1.1, 2.1, 5.1, 5.6, 5.7_

  - [ ]* 7.2 Write unit tests for DI registration
    - Test that `AddCodeCompassPipeline` registers `IPipelineOrchestrator`, `IVectorSearch`, `IEmbeddingGenerator`, `IChunkingService`
    - Test that `IEmbeddingService` resolves to `EmbeddingServiceAdapter`
    - Test that `IQuestionRouter` resolves to `KeywordQuestionRouter`
    - Test that duplicate `AddCodeCompassPipeline` calls do not register duplicates
    - _Requirements: 5.1, 5.6, 5.7_

- [x] 8. Implement Ingestion Pipeline Endpoint
  - [x] 8.1 Create IngestKnowledgeBaseRequest DTO and update IngestController
    - Create `IngestKnowledgeBaseRequest` with `TargetPath` (string) and `Mode` (IndexingMode, default Full) properties
    - Add `[HttpPost("knowledge-base")]` endpoint to `IngestController`
    - Inject `IPipelineOrchestrator` into `IngestController`
    - Map request to `PipelineRequest` and call `IPipelineOrchestrator.RunAsync`
    - Return `Ok(PipelineResult)` on success
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 8.2 Write unit tests for IngestController knowledge-base endpoint
    - Test successful ingestion returns PipelineResult with correct totals
    - Test that `DirectoryNotFoundException` propagates (handled by middleware)
    - Test partial error ingestion returns PipelineResult with error count
    - _Requirements: 4.4, 4.5, 4.6_

- [x] 9. Update GlobalExceptionMiddleware for new exception types
  - [x] 9.1 Add exception type mappings to GlobalExceptionMiddleware
    - Map `HttpRequestException` → 503 with "service temporarily unavailable" detail
    - Map `TaskCanceledException` → 503 with "service timeout" detail
    - Map `DirectoryNotFoundException` → 404 with "directory not found" detail including the path
    - Ensure all error responses use RFC 7807 ProblemDetails format
    - Ensure `ArgumentException` → 400 mapping exists
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 9.2 Write unit tests for GlobalExceptionMiddleware mappings
    - Test each exception type maps to the correct HTTP status code and ProblemDetails format
    - Test that detail messages are informative and include relevant context
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Wire all components together and verify end-to-end flow
  - [x] 11.1 Verify end-to-end chat flow integration
    - Ensure `Program.cs` / startup calls `AddInfrastructureServices(configuration)` which triggers all registrations
    - Verify the full chat pipeline: ChatController → SendChatMessageHandler → QuestionRouter → IVectorSearch → LLM → CitationMapping → Response
    - Verify error scenarios flow through GlobalExceptionMiddleware correctly
    - Ensure RequestLoggingMiddleware logs method, path, status code, and elapsed time for each request
    - _Requirements: 1.2, 2.2, 3.1, 6.3, 8.5_

  - [ ]* 11.2 Write integration tests for end-to-end chat and ingestion flows
    - Test full chat request with mocked Azure services returns properly formatted ChatResponse with citations
    - Test ingestion endpoint triggers pipeline orchestrator and returns PipelineResult
    - Test configuration validation fails startup with missing values
    - Test LLM timeout (30 second request timeout) returns 503
    - _Requirements: 5.5, 6.3, 6.4, 8.1, 8.2, 8.3_

- [ ] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit
- Unit tests validate specific examples and edge cases
- The project uses C# with ASP.NET Core 9, Clean Architecture, and CQRS patterns
- `AddCodeCompassPipeline` is the single entry point for registering all CodeCompass.RAG services
- The EmbeddingServiceAdapter maintains backward compatibility for existing IEmbeddingService consumers
- IVectorSearch handles embedding generation internally, so the handler does NOT call IEmbeddingService for search

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1"] },
    { "id": 1, "tasks": ["1.3", "2.2", "3.1"] },
    { "id": 2, "tasks": ["2.3", "2.4", "3.2", "5.1"] },
    { "id": 3, "tasks": ["5.2", "5.3"] },
    { "id": 4, "tasks": ["5.4", "5.5"] },
    { "id": 5, "tasks": ["5.6", "5.7", "5.8", "5.9", "5.10"] },
    { "id": 6, "tasks": ["7.1"] },
    { "id": 7, "tasks": ["7.2", "8.1", "9.1"] },
    { "id": 8, "tasks": ["8.2", "9.2"] },
    { "id": 9, "tasks": ["11.1"] },
    { "id": 10, "tasks": ["11.2"] }
  ]
}
```
