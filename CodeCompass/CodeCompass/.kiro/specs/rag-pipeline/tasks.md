# Tasks

## Task 1: Project Setup and Core Infrastructure

- [x] 1.1 Create the .NET 8 solution structure with projects: `CodeCompass.Core` (interfaces and models), `CodeCompass.Pipeline` (implementations), `CodeCompass.Tests.Unit` (xUnit), `CodeCompass.Tests.Property` (FsCheck + xUnit), and `CodeCompass.Tests.Integration` (xUnit + Testcontainers).
- [x] 1.2 Define all shared data models and records in `CodeCompass.Core/Models/`: `ParsedDocument`, `ParsedCode`, `Heading`, `CodeSymbol`, `CodeSymbolKind`, `SourceFileMetadata`, `Chunk`, `ChunkMetadata`, `ChunkingOptions`, `VectorDocument`, `SearchRequest`, `SearchFilter`, `SearchResult`, `SearchHit`, `PipelineRequest`, `PipelineResult`, `IndexingMode`, `IndexingPlan`, `PipelineError`, `PipelineErrorSeverity`.
- [x] 1.3 Define all core interfaces in `CodeCompass.Core/Interfaces/`: `IDocumentParser`, `ICodeParser`, `IChunkingService`, `IEmbeddingGenerator`, `IVectorStore`, `IVectorSearch`, `IMetadataExtractor`, `IIncrementalIndexingService`, `IPipelineOrchestrator`.
- [x] 1.4 Define configuration models in `CodeCompass.Core/Configuration/`: `PipelineConfiguration`, `AzureOpenAISettings`, `AzureSearchSettings`, `IngestionSettings` with the defaults specified in the design (EmbeddingDimension=1536, BatchSize=100, ConcurrencyLevel=4, EmbeddingBatchSize=16, MaxFileSizeMB=50).
- [x] 1.5 Add NuGet package references: `Azure.AI.OpenAI`, `Azure.Search.Documents`, `DocumentFormat.OpenXml`, `PdfPig` (for PDF parsing), `Microsoft.CodeAnalysis.CSharp` (Roslyn for C# parsing), `FsCheck.Xunit`, `FluentAssertions`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`.
- [x] 1.6 Implement a shared retry utility class `RetryPolicy` with exponential backoff (base delay 1s, max delay 8s, multiplier 2x, max attempts 3) that retries on HTTP 429, 500, 502, 503, 504 and does not retry on 401, 400, 404.

## Task 2: Document Parsing

- [x] 2.1 Implement `MarkdownParser : IDocumentParser` that extracts full text content and headings (levels 1–6) from Markdown files using regex-based heading detection for `#` through `######` markers.
- [x] 2.2 Implement `PdfParser : IDocumentParser` that extracts text content from all pages in page order using PdfPig, returning empty text and empty headings list for image-only PDFs.
- [x] 2.3 Implement `DocxParser : IDocumentParser` that extracts text content, headings with levels, and preserves paragraph boundaries using DocumentFormat.OpenXml.
- [-] 2.4 Implement file validation logic shared across parsers: verify file extension is in supported set (.md, .pdf, .docx), verify file size ≤ 50 MB, and return appropriate error messages for unsupported extensions or oversized files.
- [x] 2.5 Write property test `ParserOutputStructureProperty` (Property 1): for any valid input file that is successfully parsed, the output contains non-null raw text, a headings list (possibly empty), and complete source file metadata with all fields populated.
- [x] 2.6 Write property test `FileValidationAndDispatchProperty` (Property 2): for any file path, if extension is supported and size ≤ 50 MB the parser accepts it; unsupported extension returns extension error; oversized file returns size error.
- [x] 2.7 Write property test `MarkdownHeadingExtractionProperty` (Property 3): for any Markdown document with heading markers, the parser extracts each heading with correct level and text, and heading count equals marker count in source.

## Task 3: Code Repository Parsing

- [x] 3.1 Implement `CSharpCodeParser : ICodeParser` that extracts classes, methods, and XML documentation comments from C# source files using Roslyn syntax analysis.
- [x] 3.2 Implement `ReactCodeParser : ICodeParser` that extracts components, hooks, and JSDoc comment blocks from JSX/TSX files using regex-based pattern matching.
- [x] 3.3 Implement `SqlCodeParser : ICodeParser` that extracts stored procedure names, parameters, and comment blocks from SQL files using regex-based pattern matching.
- [x] 3.4 Implement `RepositoryEnumerator` that recursively enumerates files with extensions .cs, .jsx, .tsx, .sql, .js, .ts, .asp within a given directory path, with validation that the path exists and is accessible.
- [x] 3.5 Write property test `RepositoryFileEnumerationProperty` (Property 4): for any directory tree, file enumeration returns exactly those files with supported code extensions and no files with other extensions.
- [x] 3.6 Write property test `CodeParsingResilienceProperty` (Property 5): for any batch of source files where some contain syntax errors, the parser produces results for all parseable files and logs warnings for unparseable files without halting the batch.

## Task 4: Intelligent Chunking

- [x] 4.1 Implement `ChunkingService : IChunkingService` method `ChunkDocument` that splits parsed documents at paragraph boundaries, respects configurable max/min token sizes (defaults 512/50), applies configurable overlap (default 50 tokens), and assigns sequential zero-based chunk indices.
- [x] 4.2 Implement `ChunkingService` method `ChunkCode` that splits parsed code at logical boundaries (class, method, function, procedure blocks), respects configurable max/min token sizes, applies configurable overlap, and assigns sequential zero-based chunk indices.
- [x] 4.3 Implement oversized unit handling: when a single logical unit exceeds max chunk size, split at sentence boundaries (documents) or statement boundaries (code), and prepend nearest ancestor heading or enclosing declaration signature as context header to each sub-chunk.
- [x] 4.4 Implement a simple token counting utility (whitespace-based approximation or tiktoken-based) used by the chunking service to enforce token bounds.
- [x] 4.5 Write property test `DocumentChunkingParagraphBoundaryProperty` (Property 6): for any parsed document where individual paragraphs fit within max chunk size, no chunk boundary falls within a paragraph.
- [x] 4.6 Write property test `CodeChunkingLogicalBoundaryProperty` (Property 7): for any parsed code where individual logical units fit within max chunk size, no chunk boundary falls within a logical unit.
- [x] 4.7 Write property test `ChunkSizeBoundsProperty` (Property 8): for any input and valid chunking config, every chunk has token count ≥ min and ≤ max.
- [x] 4.8 Write property test `ChunkOverlapProperty` (Property 9): for any output with 2+ chunks, overlap between adjacent chunks equals configured overlap size, and overlap ≤ 25% of max chunk size.
- [x] 4.9 Write property test `SequentialChunkIndexingProperty` (Property 10): for any chunking output, indices form a zero-based contiguous sequence and each chunk references the correct source file path.
- [x] 4.10 Write property test `OversizedUnitSplittingProperty` (Property 11): for any logical unit exceeding max chunk size, splitting occurs at sentence/statement boundaries and each sub-chunk is prepended with a context header.

## Task 5: Metadata Extraction

- [x] 5.1 Implement `MetadataExtractor : IMetadataExtractor` that produces `ChunkMetadata` objects conforming to the VectorStore schema, populating source file path, chunk index, content type ("document" or "code"), language, last modified timestamp, and section heading.
- [x] 5.2 Implement document metadata logic: assign nearest ancestor heading hierarchy to each chunk based on its position within the document's heading structure.
- [x] 5.3 Implement code metadata logic: additionally populate programming language, namespace/module name, and containing class/component for code files.
- [x] 5.4 Write property test `MetadataExtractionCompletenessProperty` (Property 19): for any parsed content, the metadata extractor produces a ChunkMetadata with all VectorStore schema fields populated, with correct heading hierarchy for documents and language/namespace/class for code.

## Task 6: Embedding Generation

- [x] 6.1 Implement `AzureOpenAIEmbeddingGenerator : IEmbeddingGenerator` with `GenerateEmbeddingAsync` for single chunk embedding and `GenerateEmbeddingsBatchAsync` for batch processing using the configured batch size (default 16).
- [x] 6.2 Implement vector normalization to unit length (L2 norm = 1.0 ± 1e-6) applied to all output vectors before returning.
- [x] 6.3 Integrate retry logic using the shared `RetryPolicy` for rate-limit (429) and transient errors, with non-recoverable errors logged and chunk skipped without halting the pipeline.
- [x] 6.4 Write property test `BatchingCorrectnessProperty` (Property 12): for any list of N items and batch size B, batching produces ⌈N/B⌉ groups each with ≤ B items, every item in exactly one batch, order preserved.
- [x] 6.5 Write property test `VectorNormalizationProperty` (Property 13): for any embedding vector produced, the L2 norm equals 1.0 within floating-point tolerance of ±1e-6.

## Task 7: Vector Storage

- [x] 7.1 Implement `AzureAISearchVectorStore : IVectorStore` with `UpsertAsync` that batches upsert operations (configurable batch size, default 100) to Azure AI Search, using deterministic document ID generation.
- [x] 7.2 Implement `DeleteBySourceFileAsync` that removes all chunks associated with a given source file path from the index.
- [x] 7.3 Implement deterministic document ID generation: `Base64Url(SHA256(sourceFilePath + "|" + chunkIndex))` ensuring same file+chunk always maps to same document ID.
- [x] 7.4 Implement vector dimension validation: accept vectors matching configured embedding dimension, reject mismatched vectors with a validation error.
- [x] 7.5 Integrate retry logic using the shared `RetryPolicy` for write failures, logging at error severity with affected chunk's source file path and chunk index on final failure.
- [x] 7.6 Write property test `DeterministicDocumentIdProperty` (Property 14): for any source file path and chunk index pair, the ID is deterministic (identical inputs → identical IDs) and collision-resistant (distinct inputs → distinct IDs).
- [x] 7.7 Write property test `VectorDimensionValidationProperty` (Property 15): vectors matching configured dimension are accepted; mismatched vectors are rejected with validation error.

## Task 8: Semantic Search and Retrieval

- [x] 8.1 Implement `AzureAISearchVectorSearch : IVectorSearch` with `SearchAsync` that generates a query embedding, executes vector similarity search against Azure AI Search, and returns top-K results ordered by descending relevance score.
- [x] 8.2 Implement metadata filtering: restrict results by content type, language, or source path prefix when a `SearchFilter` is provided.
- [x] 8.3 Implement input validation: query length 1–2000 characters, K in [1, 50], reject invalid values with descriptive errors. Return empty result set with count zero when no matches found.
- [x] 8.4 Integrate retry logic for search failures (embedding generation failure or index unavailability), returning structured error results without partial result sets.
- [x] 8.5 Write property test `InputValidationProperty` (Property 16): query length 1–2000 accepted, 0 or >2000 rejected; K in [1,50] accepted, outside rejected; mode only "full"/"incremental" accepted.
- [x] 8.6 Write property test `SearchResultOrderingProperty` (Property 17): results ordered by descending relevance score in [0.0, 1.0], each hit has non-empty chunk text and all metadata fields.
- [x] 8.7 Write property test `MetadataFilterProperty` (Property 18): with a filter applied, every returned hit satisfies the filter criteria.

## Task 9: Incremental Indexing

- [x] 9.1 Implement `IncrementalIndexingService : IIncrementalIndexingService` with JSON-based state persistence (version, lastRunTimestamp, files map with lastModified and chunkCount per file).
- [x] 9.2 Implement `ComputePlanAsync` that compares current file timestamps against stored state to classify files as new, modified, deleted, or unchanged.
- [x] 9.3 Implement `UpdateStateAsync` and `RemoveStateAsync` for updating state only after successful processing and removing state for deleted files.
- [x] 9.4 Handle corrupted/missing state file: perform full re-index and regenerate state record when state is unparseable or missing.
- [x] 9.5 Write property test `IndexingStateRoundTripProperty` (Property 20): serializing and deserializing any valid indexing state record produces an equivalent record with identical paths, timestamps, and chunk counts.
- [x] 9.6 Write property test `ChangeDetectionClassificationProperty` (Property 21): for any set of current files and stored state, classification is correct (new/modified/deleted/unchanged) and the union of all categories equals the union of files on disk and in state.

## Task 10: Pipeline Orchestration

- [x] 10.1 Implement `PipelineOrchestrator : IPipelineOrchestrator` that coordinates the full pipeline: accepts `PipelineRequest` with target path and mode, dispatches to full or incremental flow, runs all stages (parse → chunk → metadata → embed → store), and returns `PipelineResult`.
- [x] 10.2 Implement parallel file processing with configurable concurrency level (1–32, default 4) using `SemaphoreSlim` or `Parallel.ForEachAsync`.
- [x] 10.3 Implement file-level error isolation: individual file failures are logged and skipped without halting the pipeline. Fatal errors (target path inaccessible, Azure services unavailable after retries) halt the pipeline and return a partial summary.
- [x] 10.4 Implement the full indexing flow: enumerate all supported files in target path, process each through the complete pipeline, update indexing state for successfully processed files.
- [x] 10.5 Implement the incremental indexing flow: compute indexing plan, process new/modified files, delete chunks for removed files, skip unchanged files, update state.
- [x] 10.6 Write property test `FailedFilesDontCorruptStateProperty` (Property 22): for any batch where some files fail, the pipeline continues, does not update state for failed files, and produces results for non-failed files.
- [x] 10.7 Write property test `SummaryCountsConsistencyProperty` (Property 23): newly indexed + re-indexed + deleted + skipped + failed = total files considered, all counts non-negative, elapsed time > 0.
- [x] 10.8 Write property test `FullModeProcessesAllSupportedFilesProperty` (Property 24): for any directory tree and full indexing request, every file with a supported extension is processed and no unsupported-extension files are processed.

## Task 11: Dependency Injection and Integration

- [x] 11.1 Create a DI registration extension method `AddCodeCompassPipeline(this IServiceCollection services, IConfiguration configuration)` that registers all services, binds configuration from `appsettings.json`, and wires up logging.
- [x] 11.2 Create a sample `appsettings.json` with all configuration sections (AzureOpenAI, AzureSearch, Chunking, Ingestion) with placeholder values and documentation comments.
- [ ] 11.3 Write unit tests verifying DI container resolves all interfaces to their implementations and configuration binding maps settings correctly.
