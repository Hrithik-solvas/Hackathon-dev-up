# Requirements Document

## Introduction

This specification defines the requirements for a comprehensive README.md file for the CodeCompass project. The README targets internal contributors (developers working on the CodeCompass codebase) and covers project architecture, module descriptions, usage instructions, documentation ingestion format, and query-side integration. The README references a full sample project for end-to-end demonstration rather than inline code snippets.

## Glossary

- **README**: The top-level `README.md` file located at the repository root of CodeCompass
- **CodeCompass**: The .NET 8.0 RAG (Retrieval-Augmented Generation) solution that indexes documentation and code files into a vector store for semantic search
- **Module**: A distinct .NET project within the CodeCompass solution (e.g., Core, Chunking, Pipeline, Search)
- **Pipeline**: The ingestion pipeline that processes files through stages: Parse → Chunk → Metadata → Embed → Store
- **Search_API**: The query-side interface (`IVectorSearch.SearchAsync`) used to retrieve indexed content via semantic similarity
- **Ingestion_Format**: The set of supported file formats (.md, .pdf, .docx, .cs, .jsx, .tsx, .sql) and structural requirements for documents to be indexed
- **Contributor**: An internal developer working on, extending, or onboarding to the CodeCompass codebase
- **Sample_Project**: A referenced end-to-end demonstration project that shows the full ingest-to-query workflow

## Requirements

### Requirement 1: Project Overview Section

**User Story:** As a Contributor, I want a clear project overview in the README, so that I can quickly understand what CodeCompass does and its architectural purpose.

#### Acceptance Criteria

1. THE README SHALL contain an introductory section that describes CodeCompass as a .NET 8.0 RAG pipeline for indexing documentation and code into a vector store for semantic search.
2. THE README SHALL list the technology stack including .NET 8.0, Azure OpenAI (text-embedding-ada-002, 1536 dimensions), and Azure AI Search.
3. THE README SHALL include a high-level architecture diagram or textual description of the pipeline stages (Parse → Chunk → Metadata → Embed → Store).

### Requirement 2: Module Descriptions Section

**User Story:** As a Contributor, I want each project/module described with its responsibility, so that I understand the codebase structure and where to make changes.

#### Acceptance Criteria

1. THE README SHALL contain a dedicated section that lists every module in the solution.
2. THE README SHALL describe the CodeCompass.Core module as the shared contracts library containing interfaces, models, configuration records, and DI registration extensions.
3. THE README SHALL describe the CodeCompass.Chunking module as the service responsible for splitting parsed documents and code into token-bounded chunks with configurable overlap.
4. THE README SHALL describe the CodeCompass.Pipeline module as the orchestrator that coordinates the full indexing workflow with parallel processing and configurable concurrency.
5. THE README SHALL describe the CodeCompass.Search module as the Azure AI Search integration providing vector storage and semantic similarity search.
6. THE README SHALL describe referenced modules (Parsing, Embedding, Storage, Indexing) with a brief summary of each module's responsibility.
7. WHEN a module exposes a primary interface, THE README SHALL name that interface (e.g., IPipelineOrchestrator, IVectorSearch, IChunkingService).

### Requirement 3: Getting Started and Usage Section

**User Story:** As a Contributor, I want setup and usage instructions, so that I can build and run CodeCompass locally for development.

#### Acceptance Criteria

1. THE README SHALL list prerequisites for building the project including .NET 8.0 SDK, an Azure OpenAI resource, and an Azure AI Search resource.
2. THE README SHALL document how to configure the application via `appsettings.json` with sections: AzureOpenAI, AzureSearch, Chunking, and Ingestion.
3. THE README SHALL describe DI registration using the `AddPipelineConfiguration` extension method.
4. THE README SHALL explain the two indexing modes (Full and Incremental) and when to use each mode.
5. THE README SHALL reference a Sample_Project that demonstrates the complete end-to-end workflow (ingest documentation → query results) rather than providing inline code snippets.

### Requirement 4: Documentation Ingestion Format Section

**User Story:** As a Contributor, I want to know what file formats and structures are required for documentation ingestion, so that I can prepare content for indexing.

#### Acceptance Criteria

1. THE README SHALL contain a dedicated section describing supported ingestion formats.
2. THE README SHALL identify Markdown (.md) as the primary documentation format with detailed guidance on structuring headings for optimal chunking.
3. THE README SHALL describe how headings in Markdown files are used to derive section context and chunk metadata during parsing.
4. THE README SHALL briefly mention additional supported document formats (.pdf, .docx) with a note on their parsing behavior.
5. THE README SHALL briefly mention supported code file formats (.cs, .jsx, .tsx, .sql) and describe how code symbols (classes, methods, components, hooks, stored procedures) are extracted during parsing.
6. THE README SHALL document the chunking parameters (MaxTokens: 512, MinTokens: 50, OverlapTokens: 50) and their effect on content segmentation.
7. THE README SHALL specify the maximum file size limit (50 MB) enforced by the ingestion pipeline.

### Requirement 5: Query-Side Integration Section

**User Story:** As a Contributor, I want documentation on how to query indexed data, so that I can integrate CodeCompass search into consuming applications.

#### Acceptance Criteria

1. THE README SHALL contain a dedicated section describing query-side integration using the `IVectorSearch` interface.
2. THE README SHALL document the `SearchAsync` method signature including the `SearchRequest` parameter and `CancellationToken`.
3. THE README SHALL describe the `SearchRequest` record fields: Query (string), TopK (integer, range 1-50, default 5), and optional Filter.
4. THE README SHALL describe the `SearchFilter` record fields: ContentType, Language, and SourcePathPrefix, all optional.
5. THE README SHALL describe the `SearchResult` record containing a collection of `SearchHit` items and a TotalCount.
6. THE README SHALL describe the `SearchHit` record fields: ChunkText (the matched content), RelevanceScore (float indicating similarity), and Metadata (source file path, chunk index, content type, language, last modified date, section heading).
7. THE README SHALL explain how to interpret RelevanceScore values for determining result quality.

### Requirement 6: Configuration Reference Section

**User Story:** As a Contributor, I want a complete configuration reference, so that I can tune pipeline behavior for different environments.

#### Acceptance Criteria

1. THE README SHALL document all AzureOpenAI configuration settings: Endpoint, DeploymentName, ApiKey, and EmbeddingDimension (default: 1536).
2. THE README SHALL document all AzureSearch configuration settings: Endpoint, IndexName, ApiKey, and BatchSize (default: 100).
3. THE README SHALL document all Chunking configuration settings: MaxTokens (default: 512), MinTokens (default: 50), and OverlapTokens (default: 50).
4. THE README SHALL document all Ingestion configuration settings: ConcurrencyLevel (default: 4, range: 1-32), EmbeddingBatchSize (default: 16), and MaxFileSizeMB (default: 50).

### Requirement 7: Sample Project Reference

**User Story:** As a Contributor, I want a reference to a sample project, so that I can see a working end-to-end example without piecing together inline snippets.

#### Acceptance Criteria

1. THE README SHALL reference a sample project location within the repository or as a linked resource.
2. THE README SHALL describe what the sample project demonstrates: configuring services, running the ingestion pipeline on a document folder, and querying results via SearchAsync.
3. THE README SHALL indicate the expected output of the sample project (PipelineResult summary and SearchHit results).

### Requirement 8: Document Structure and Navigation

**User Story:** As a Contributor, I want the README to be well-structured and navigable, so that I can find relevant information quickly.

#### Acceptance Criteria

1. THE README SHALL include a table of contents with anchor links to each major section.
2. THE README SHALL use consistent Markdown heading levels (H2 for major sections, H3 for subsections).
3. THE README SHALL place the project overview and architecture description before module details and integration sections.
