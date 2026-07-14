namespace CodeCompass.Core.Models;

/// <summary>
/// Represents a request to run the indexing pipeline.
/// </summary>
public record PipelineRequest(string TargetPath, IndexingMode Mode);

/// <summary>
/// Summary of a completed pipeline run.
/// </summary>
public record PipelineResult(
    int TotalFilesProcessed,
    int TotalChunksGenerated,
    int TotalErrors,
    long ElapsedMilliseconds,
    int FilesNewlyIndexed,
    int FilesReIndexed,
    int FilesDeleted,
    int FilesSkipped,
    int FilesFailed);

/// <summary>
/// The result of parsing a document file (Markdown, PDF, DOCX).
/// </summary>
public record ParsedDocument(
    string RawText,
    IReadOnlyList<Heading> Headings,
    SourceFileMetadata SourceMetadata);

/// <summary>
/// The result of parsing a code file (C#, JSX/TSX, SQL).
/// </summary>
public record ParsedCode(
    string RawText,
    IReadOnlyList<CodeSymbol> Symbols,
    IReadOnlyList<string> DocumentationComments,
    SourceFileMetadata SourceMetadata);

/// <summary>
/// A heading extracted from a document, with its nesting level.
/// </summary>
public record Heading(int Level, string Text);

/// <summary>
/// Metadata about the source file from which content was parsed.
/// </summary>
public record SourceFileMetadata(
    string FilePath,
    string FileName,
    string FileExtension,
    DateTimeOffset LastModified);

/// <summary>
/// A symbol extracted from a code file.
/// </summary>
public record CodeSymbol(string Name, CodeSymbolKind Kind, string? ParentName);

/// <summary>
/// A chunk of text produced by the chunking service.
/// </summary>
public record Chunk(
    string Text,
    int Index,
    string? ContextHeader,
    ChunkMetadata Metadata);

/// <summary>
/// Metadata associated with a chunk for storage and retrieval.
/// </summary>
public record ChunkMetadata(
    string SourceFilePath,
    int ChunkIndex,
    string ContentType,
    string? Language,
    DateTimeOffset LastModified,
    string? SectionHeading);

/// <summary>
/// Options that control chunking behavior.
/// </summary>
public record ChunkingOptions(
    int MaxTokens = 512,
    int MinTokens = 50,
    int OverlapTokens = 50);

/// <summary>
/// A document ready to be stored in the vector index.
/// </summary>
public record VectorDocument(
    string Id,
    float[] Embedding,
    string ChunkText,
    ChunkMetadata Metadata);

/// <summary>
/// A request to search the vector index.
/// </summary>
public record SearchRequest(
    string Query,
    int TopK = 5,
    SearchFilter? Filter = null);

/// <summary>
/// Filter criteria for narrowing search results.
/// </summary>
public record SearchFilter(
    string? ContentType = null,
    string? Language = null,
    string? SourcePathPrefix = null);

/// <summary>
/// The result of a vector search query.
/// </summary>
public record SearchResult(
    IReadOnlyList<SearchHit> Hits,
    int TotalCount);

/// <summary>
/// A single hit from a vector search query.
/// </summary>
public record SearchHit(
    string ChunkText,
    float RelevanceScore,
    ChunkMetadata Metadata);

/// <summary>
/// The plan computed by incremental indexing describing what actions to take.
/// </summary>
public record IndexingPlan(
    IReadOnlyList<string> NewFiles,
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<string> DeletedFiles,
    IReadOnlyList<string> UnchangedFiles);

/// <summary>
/// Represents an error that occurred during pipeline processing.
/// </summary>
public record PipelineError(
    PipelineErrorSeverity Severity,
    string FilePath,
    string Stage,
    string Message,
    Exception? InnerException);
