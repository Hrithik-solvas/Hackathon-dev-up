# Requirements Document

## Introduction

CodeCompass is a Retrieval Augmented Generation (RAG) pipeline designed for enterprise documentation environments. The system ingests documents (Markdown, PDF, DOCX) and code repositories (C#, React, SQL stored procedures), parses them intelligently, generates embeddings via Azure OpenAI, stores vectors in Azure AI Search, and retrieves semantically relevant chunks in response to user queries. The pipeline supports incremental indexing to efficiently handle updates without full re-processing.

## Glossary

- **RAG_Pipeline**: The end-to-end system that ingests documents and code, generates embeddings, stores vectors, and retrieves relevant content for augmented generation.
- **DocumentParser**: The component responsible for extracting raw text and structure from supported document formats (Markdown, PDF, DOCX).
- **ChunkingService**: The component responsible for splitting parsed content into semantically coherent segments suitable for embedding.
- **EmbeddingGenerator**: The component responsible for converting text chunks into vector representations using Azure OpenAI embedding models.
- **VectorStore**: The Azure AI Search index that persists vector embeddings and associated metadata for retrieval.
- **VectorSearch**: The component responsible for executing semantic similarity queries against the VectorStore and returning ranked results.
- **MetadataExtractor**: The component responsible for extracting contextual metadata (file path, language, section headers, authorship) from documents and code files.
- **Incremental_Indexing**: The process of detecting changed or new documents since the last indexing run and processing only those items, avoiding full re-ingestion.
- **Chunk**: A discrete segment of text or code with associated metadata, used as the unit of embedding and retrieval.
- **Semantic_Search**: A search approach that matches user queries to stored content based on meaning rather than exact keyword matches.

## Requirements

### Requirement 1: Document Parsing

**User Story:** As a developer, I want the system to parse enterprise documents in multiple formats, so that I can index all relevant documentation for retrieval.

#### Acceptance Criteria

1. WHEN a Markdown file is provided, THE DocumentParser SHALL extract the full text content and represent each heading with its level (e.g., H1, H2, H3) and text in the parsed output.
2. WHEN a PDF file is provided, THE DocumentParser SHALL extract the text content from all pages in page order.
3. WHEN a DOCX file is provided, THE DocumentParser SHALL extract the text content and represent each heading with its level and text, and preserve paragraph boundaries in the parsed output.
4. IF a file with an unsupported format is provided, THEN THE DocumentParser SHALL return an error indicating the provided file extension and that it is not in the supported format list (Markdown, PDF, DOCX).
5. THE DocumentParser SHALL expose the parsed content as a structured object containing: the raw extracted text, a list of detected headings each with level and text, and source file metadata including file path, file name, file extension, and last modified timestamp.
6. IF a PDF file contains no extractable text (e.g., image-only pages), THEN THE DocumentParser SHALL return a structured result with empty raw text and an empty headings list rather than an error.
7. THE DocumentParser SHALL identify the file format based on the file extension and accept files up to 50 MB in size. IF a file exceeds 50 MB, THEN THE DocumentParser SHALL return an error indicating the file exceeds the maximum supported size.

### Requirement 2: Code Repository Parsing

**User Story:** As a developer, I want the system to parse code from Git repositories, so that I can search across codebases for relevant implementations.

#### Acceptance Criteria

1. WHEN a Git repository path is provided, THE DocumentParser SHALL recursively enumerate all files with extensions .cs, .jsx, .tsx, and .sql within the repository directory tree and parse each file according to its language type.
2. WHEN a C# source file is provided, THE DocumentParser SHALL extract classes, methods, and XML documentation comments.
3. WHEN a React (JSX/TSX) source file is provided, THE DocumentParser SHALL extract components, hooks, and JSDoc comment blocks.
4. WHEN a SQL stored procedure file is provided, THE DocumentParser SHALL extract procedure names, parameters, and comment blocks.
5. IF a source file cannot be parsed due to syntax errors, THEN THE DocumentParser SHALL log a warning containing the file path and error description, and continue processing remaining files.
6. IF the provided Git repository path does not exist or is not accessible, THEN THE DocumentParser SHALL return a descriptive error indicating the path is invalid or inaccessible.
7. THE DocumentParser SHALL expose parsed code content as a structured object containing raw text, extracted symbols (classes, methods, components, hooks, or procedures), documentation comments, and source file metadata.

### Requirement 3: Intelligent Chunking

**User Story:** As a developer, I want documents and code to be split into semantically meaningful chunks, so that retrieval returns focused, relevant segments rather than entire files.

#### Acceptance Criteria

1. WHEN parsed document content is provided, THE ChunkingService SHALL split the content into chunks that do not break within a paragraph or section boundary, preserving complete paragraphs as atomic units where they fit within the maximum chunk size.
2. WHEN parsed code content is provided, THE ChunkingService SHALL split the content at logical boundaries: class definitions, method definitions, function definitions, and procedure blocks, keeping each logical unit as a single chunk where it fits within the maximum chunk size.
3. THE ChunkingService SHALL produce chunks with a configurable maximum token size with a default of 512 tokens and a configurable minimum token size with a default of 50 tokens, discarding any resulting segment below the minimum.
4. THE ChunkingService SHALL include overlap between adjacent chunks with a configurable overlap size defaulting to 50 tokens, where the overlap size must not exceed 25% of the configured maximum chunk size.
5. THE ChunkingService SHALL preserve the association between each chunk and its source metadata, and assign each chunk a sequential zero-based index representing its position within the source document.
6. IF a single logical unit exceeds the maximum chunk size, THEN THE ChunkingService SHALL split the unit at sentence boundaries for document content or statement boundaries for code content, and prepend the nearest ancestor heading or enclosing declaration signature to each resulting sub-chunk as a context header.
7. IF the parsed content provided to the ChunkingService is empty or contains no extractable text, THEN THE ChunkingService SHALL return an empty chunk list without error.

### Requirement 4: Embedding Generation

**User Story:** As a developer, I want text chunks to be converted into vector embeddings, so that semantic similarity search is possible.

#### Acceptance Criteria

1. WHEN a text chunk is provided, THE EmbeddingGenerator SHALL call Azure OpenAI embedding API and return a vector representation.
2. THE EmbeddingGenerator SHALL support batch processing of multiple chunks in a single API call to optimize throughput.
3. IF the Azure OpenAI API returns a rate-limit error, THEN THE EmbeddingGenerator SHALL retry the request with exponential backoff up to 3 attempts.
4. IF the Azure OpenAI API returns a non-recoverable error, THEN THE EmbeddingGenerator SHALL log the error and skip the affected chunk without halting the pipeline.
5. THE EmbeddingGenerator SHALL normalize all output vectors to unit length before storage.

### Requirement 5: Vector Storage

**User Story:** As a developer, I want embeddings and metadata stored in a vector database, so that they can be efficiently retrieved at query time.

#### Acceptance Criteria

1. WHEN an embedding and its associated metadata are provided, THE VectorStore SHALL persist the vector, chunk text, and metadata in an Azure AI Search index.
2. WHEN a chunk is indexed whose source file path and chunk index match an existing document in the index, THE VectorStore SHALL replace the existing document with the new embedding, chunk text, and metadata.
3. THE VectorStore SHALL store the following metadata fields for each chunk: source file path (maximum 1024 characters), chunk index (zero-based integer), content type, language, last modified timestamp (ISO 8601 UTC format), and section heading (maximum 512 characters).
4. IF a write operation to Azure AI Search fails, THEN THE VectorStore SHALL retry the operation up to 3 times with exponential backoff starting at a base delay of 1 second and capping at 8 seconds.
5. IF all retry attempts fail, THEN THE VectorStore SHALL log the failure at error severity with the affected chunk's source file path and chunk index, and continue processing remaining chunks in the batch.
6. THE VectorStore SHALL accept vectors with a dimensionality matching the configured embedding model output size and reject any vector whose dimensionality does not match.

### Requirement 6: Semantic Search and Retrieval

**User Story:** As a developer, I want to query the system with natural language and receive the most relevant chunks, so that I can augment LLM prompts with accurate context.

#### Acceptance Criteria

1. WHEN a natural language query of 1 to 2000 characters is provided, THE VectorSearch SHALL generate an embedding for the query using the EmbeddingGenerator.
2. WHEN a query embedding is generated, THE VectorSearch SHALL execute a vector similarity search against the Azure AI Search index and return the top-K most similar chunks ordered by descending relevance score.
3. THE VectorSearch SHALL support a configurable K value between 1 and 50 inclusive, with a default of 5 results.
4. THE VectorSearch SHALL return each result with a relevance score normalized to the range 0.0 to 1.0, the chunk text, and the metadata fields defined in the VectorStore schema (source file path, chunk index, content type, language, last modified timestamp, and section heading).
5. WHERE a metadata filter is provided, THE VectorSearch SHALL restrict search results to chunks matching the specified filter criteria (e.g., content type, language, source path).
6. IF the EmbeddingGenerator fails to produce a query embedding, THEN THE VectorSearch SHALL return an error indication describing the failure without returning partial results.
7. IF the Azure AI Search index is unreachable or returns an error during the similarity search, THEN THE VectorSearch SHALL retry the request up to 3 times with exponential backoff, and if all attempts fail, return an error indication describing the failure.
8. IF the query results in zero matching chunks above the similarity threshold, THEN THE VectorSearch SHALL return an empty result set with a count of zero.

### Requirement 7: Metadata Extraction

**User Story:** As a developer, I want rich metadata attached to each chunk, so that I can filter and contextualize search results effectively.

#### Acceptance Criteria

1. WHEN a document is parsed, THE MetadataExtractor SHALL extract the file path, file name, file extension, and last modified timestamp.
2. WHEN a code file is parsed, THE MetadataExtractor SHALL additionally extract the programming language, namespace or module name, and containing class or component name.
3. WHEN a document contains headings, THE MetadataExtractor SHALL extract the nearest ancestor heading hierarchy for each chunk.
4. THE MetadataExtractor SHALL produce a structured metadata object that conforms to the VectorStore schema.

### Requirement 8: Incremental Indexing

**User Story:** As a developer, I want the pipeline to detect and process only new or changed files, so that re-indexing large repositories is fast and cost-efficient.

#### Acceptance Criteria

1. THE Incremental_Indexing component SHALL maintain a persistent record of each indexed file's path and last-modified timestamp that survives across indexing runs.
2. WHEN an indexing run is initiated, THE Incremental_Indexing component SHALL compare current file timestamps against the stored records to identify new, modified, or deleted files.
3. WHEN a file is new or has been modified since the last indexing run, THE Incremental_Indexing component SHALL re-process only that file through parsing, chunking, embedding, and storage.
4. WHEN a file has been deleted since the last indexing run, THE Incremental_Indexing component SHALL remove all associated chunks from the VectorStore.
5. WHEN a file has not changed since the last indexing run, THE Incremental_Indexing component SHALL skip processing for that file.
6. IF the indexing state record is missing or fails integrity validation (e.g., unparseable format or incomplete entries), THEN THE Incremental_Indexing component SHALL perform a full re-index and regenerate the state record.
7. WHEN a file is successfully processed during an indexing run, THE Incremental_Indexing component SHALL update the state record for that file only after its chunks have been persisted to the VectorStore.
8. IF processing of a file fails during an indexing run, THEN THE Incremental_Indexing component SHALL leave that file's state record unchanged so the file is retried on the next run, and SHALL continue processing remaining files.
9. WHEN an incremental indexing run completes, THE Incremental_Indexing component SHALL produce a summary indicating the count of files newly indexed, re-indexed, deleted, skipped, and failed.

### Requirement 9: Enterprise Optimization

**User Story:** As an enterprise administrator, I want the pipeline optimized for large-scale documentation sets, so that indexing and retrieval perform well under enterprise workloads.

#### Acceptance Criteria

1. THE RAG_Pipeline SHALL support parallel processing of documents during ingestion with a configurable concurrency level between 1 and 32, defaulting to 4.
2. THE RAG_Pipeline SHALL support processing repositories containing at least 10,000 files, completing the full indexing run even if individual files fail to process.
3. THE EmbeddingGenerator SHALL batch embedding requests into groups of a configurable size between 1 and 100, defaulting to 16, per Azure OpenAI API call.
4. THE VectorStore SHALL batch upsert operations into groups of a configurable size between 1 and 1000, defaulting to 100, per Azure AI Search request.
5. THE RAG_Pipeline SHALL log processing progress including files processed, files failed, chunks generated, and elapsed time for each indexing run.
6. IF a file fails to process during ingestion, THEN THE RAG_Pipeline SHALL skip the failed file, record the file path and error reason in the processing log, and continue processing the remaining files.

### Requirement 10: Pipeline Orchestration

**User Story:** As a developer, I want a unified pipeline entry point, so that I can trigger full or incremental indexing with a single call.

#### Acceptance Criteria

1. WHEN a full indexing request is initiated with a target repository or document path, THE RAG_Pipeline SHALL process all documents and code files within the specified path through parsing, chunking, embedding, and storage.
2. WHEN an incremental indexing request is initiated with a target repository or document path, THE RAG_Pipeline SHALL invoke the Incremental_Indexing component to process only changed files within the specified path.
3. THE RAG_Pipeline SHALL return a summary report containing: total files processed, total chunks generated, total errors encountered, and total elapsed time in milliseconds.
4. IF any stage of the pipeline encounters a fatal error that prevents further processing (e.g., Azure service unavailable, target path inaccessible), THEN THE RAG_Pipeline SHALL halt processing, log the error, and return a partial summary containing the same fields as the full summary report reflecting only the work completed before the failure.
5. WHEN the pipeline entry point is invoked, THE RAG_Pipeline SHALL accept a mode parameter indicating either "full" or "incremental" indexing, and a target path parameter specifying the repository or document directory to index.
