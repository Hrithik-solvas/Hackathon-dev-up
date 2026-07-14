using System.Collections.Concurrent;
using System.Diagnostics;
using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompass.Pipeline;

/// <summary>
/// Coordinates the full indexing pipeline: accepts a PipelineRequest, dispatches to full or
/// incremental flow, runs all stages (parse → chunk → metadata → embed → store), and returns PipelineResult.
/// </summary>
public class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly IEnumerable<IDocumentParser> _documentParsers;
    private readonly IEnumerable<ICodeParser> _codeParsers;
    private readonly IChunkingService _chunkingService;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly IIncrementalIndexingService _incrementalIndexingService;
    private readonly IRepositoryEnumerator _repositoryEnumerator;
    private readonly ILogger<PipelineOrchestrator> _logger;
    private readonly IngestionSettings _settings;
    private readonly int _concurrencyLevel;

    private static readonly HashSet<string> SupportedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".pdf", ".docx"
    };

    private static readonly HashSet<string> SupportedCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".jsx", ".tsx", ".sql"
    };

    /// <summary>
    /// Exception types that indicate the target path is inaccessible (fatal errors).
    /// </summary>
    private static readonly HashSet<Type> FatalPathExceptionTypes = new()
    {
        typeof(UnauthorizedAccessException),
        typeof(DirectoryNotFoundException)
    };

    public PipelineOrchestrator(
        IEnumerable<IDocumentParser> documentParsers,
        IEnumerable<ICodeParser> codeParsers,
        IChunkingService chunkingService,
        IMetadataExtractor metadataExtractor,
        IEmbeddingGenerator embeddingGenerator,
        IVectorStore vectorStore,
        IIncrementalIndexingService incrementalIndexingService,
        IRepositoryEnumerator repositoryEnumerator,
        ILogger<PipelineOrchestrator> logger,
        IOptions<IngestionSettings> settings)
    {
        _documentParsers = documentParsers ?? throw new ArgumentNullException(nameof(documentParsers));
        _codeParsers = codeParsers ?? throw new ArgumentNullException(nameof(codeParsers));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _incrementalIndexingService = incrementalIndexingService ?? throw new ArgumentNullException(nameof(incrementalIndexingService));
        _repositoryEnumerator = repositoryEnumerator ?? throw new ArgumentNullException(nameof(repositoryEnumerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        // Clamp concurrency level to [1, 32]
        _concurrencyLevel = Math.Clamp(_settings.ConcurrencyLevel, 1, 32);
        if (_concurrencyLevel != _settings.ConcurrencyLevel)
        {
            _logger.LogWarning(
                "ConcurrencyLevel {Configured} was clamped to {Actual} (valid range: 1–32).",
                _settings.ConcurrencyLevel, _concurrencyLevel);
        }
    }

    /// <inheritdoc />
    public async Task<PipelineResult> RunAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Pipeline started. Mode: {Mode}, TargetPath: {TargetPath}", request.Mode, request.TargetPath);

        // Validate request
        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            throw new ArgumentException("Target path cannot be null or empty.", nameof(request));
        }

        if (!Directory.Exists(request.TargetPath))
        {
            throw new DirectoryNotFoundException($"Target path does not exist: {request.TargetPath}");
        }

        if (!Enum.IsDefined(typeof(IndexingMode), request.Mode))
        {
            throw new ArgumentException($"Invalid indexing mode: {request.Mode}", nameof(request));
        }

        PipelineResult result;
        if (request.Mode == IndexingMode.Incremental)
        {
            result = await RunIncrementalAsync(request.TargetPath, stopwatch, cancellationToken);
        }
        else
        {
            result = await RunFullAsync(request.TargetPath, stopwatch, cancellationToken);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Pipeline completed. Processed {Total} files in {Elapsed}ms. New: {New}, ReIndexed: {ReIndexed}, Deleted: {Deleted}, Skipped: {Skipped}, Failed: {Failed}",
            result.TotalFilesProcessed, result.ElapsedMilliseconds,
            result.FilesNewlyIndexed, result.FilesReIndexed, result.FilesDeleted, result.FilesSkipped, result.FilesFailed);

        return result;
    }

    private async Task<PipelineResult> RunFullAsync(string targetPath, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var allFiles = EnumerateAllSupportedFiles(targetPath);
        _logger.LogInformation("Full indexing: found {FileCount} supported files. Concurrency: {Concurrency}", allFiles.Count, _concurrencyLevel);

        var errors = new ConcurrentBag<PipelineError>();
        int totalChunks = 0;
        int filesProcessed = 0;
        int filesFailed = 0;
        var fatalError = (PipelineError?)null;

        // Use a CancellationTokenSource to signal fatal error halt
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _concurrencyLevel,
            CancellationToken = linkedCts.Token
        };

        try
        {
            await Parallel.ForEachAsync(allFiles, parallelOptions, async (filePath, token) =>
            {
                try
                {
                    var chunkCount = await ProcessFileAsync(filePath, token);
                    Interlocked.Add(ref totalChunks, chunkCount);
                    Interlocked.Increment(ref filesProcessed);

                    // Update incremental state for successfully processed files
                    var lastModified = File.GetLastWriteTimeUtc(filePath);
                    await _incrementalIndexingService.UpdateStateAsync(filePath, lastModified, chunkCount, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex) when (IsFatalException(ex))
                {
                    // Fatal error: halt the pipeline
                    Interlocked.Increment(ref filesFailed);
                    var fatal = new PipelineError(
                        PipelineErrorSeverity.Fatal,
                        filePath,
                        "Processing",
                        ex.Message,
                        ex);
                    errors.Add(fatal);
                    Interlocked.CompareExchange(ref fatalError, fatal, null);
                    _logger.LogCritical(ex, "Fatal error processing file: {FilePath}. Halting pipeline.", filePath);
                    linkedCts.Cancel();
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref filesFailed);
                    _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                    errors.Add(new PipelineError(
                        PipelineErrorSeverity.Error,
                        filePath,
                        "Processing",
                        ex.Message,
                        ex));

                    // Check if all files in this batch are failing (indicates service-level failure)
                    CheckForServiceLevelFailure(ref fatalError, errors, filesProcessed, filesFailed, linkedCts);
                }
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Pipeline was halted due to fatal error, not user cancellation
            _logger.LogWarning("Pipeline halted due to fatal error. Returning partial results.");
        }

        stopwatch.Stop();
        return new PipelineResult(
            TotalFilesProcessed: filesProcessed + filesFailed,
            TotalChunksGenerated: totalChunks,
            TotalErrors: errors.Count,
            ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
            FilesNewlyIndexed: filesProcessed,
            FilesReIndexed: 0,
            FilesDeleted: 0,
            FilesSkipped: 0,
            FilesFailed: filesFailed);
    }

    private async Task<PipelineResult> RunIncrementalAsync(string targetPath, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var plan = await _incrementalIndexingService.ComputePlanAsync(targetPath, cancellationToken);
        _logger.LogInformation(
            "Incremental indexing plan: {New} new, {Modified} modified, {Deleted} deleted, {Unchanged} unchanged. Concurrency: {Concurrency}",
            plan.NewFiles.Count, plan.ModifiedFiles.Count, plan.DeletedFiles.Count, plan.UnchangedFiles.Count, _concurrencyLevel);

        var errors = new ConcurrentBag<PipelineError>();
        int totalChunks = 0;
        int filesNewlyIndexed = 0;
        int filesReIndexed = 0;
        int filesDeleted = 0;
        int filesFailed = 0;
        var fatalError = (PipelineError?)null;

        // Use a CancellationTokenSource to signal fatal error halt
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _concurrencyLevel,
            CancellationToken = linkedCts.Token
        };

        // Process new files in parallel
        try
        {
            await Parallel.ForEachAsync(plan.NewFiles, parallelOptions, async (filePath, token) =>
            {
                try
                {
                    var chunkCount = await ProcessFileAsync(filePath, token);
                    Interlocked.Add(ref totalChunks, chunkCount);
                    Interlocked.Increment(ref filesNewlyIndexed);

                    var lastModified = File.GetLastWriteTimeUtc(filePath);
                    await _incrementalIndexingService.UpdateStateAsync(filePath, lastModified, chunkCount, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsFatalException(ex))
                {
                    Interlocked.Increment(ref filesFailed);
                    var fatal = new PipelineError(PipelineErrorSeverity.Fatal, filePath, "Processing", ex.Message, ex);
                    errors.Add(fatal);
                    Interlocked.CompareExchange(ref fatalError, fatal, null);
                    _logger.LogCritical(ex, "Fatal error processing new file: {FilePath}. Halting pipeline.", filePath);
                    linkedCts.Cancel();
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref filesFailed);
                    _logger.LogError(ex, "Error processing new file: {FilePath}", filePath);
                    errors.Add(new PipelineError(PipelineErrorSeverity.Error, filePath, "Processing", ex.Message, ex));
                    CheckForServiceLevelFailure(ref fatalError, errors, filesNewlyIndexed + filesReIndexed, filesFailed, linkedCts);
                }
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Pipeline halted during new file processing due to fatal error.");
        }

        // Process modified files in parallel (if not halted)
        if (fatalError == null)
        {
            try
            {
                await Parallel.ForEachAsync(plan.ModifiedFiles, parallelOptions, async (filePath, token) =>
                {
                    try
                    {
                        // Delete old chunks first
                        await _vectorStore.DeleteBySourceFileAsync(filePath, token);

                        var chunkCount = await ProcessFileAsync(filePath, token);
                        Interlocked.Add(ref totalChunks, chunkCount);
                        Interlocked.Increment(ref filesReIndexed);

                        var lastModified = File.GetLastWriteTimeUtc(filePath);
                        await _incrementalIndexingService.UpdateStateAsync(filePath, lastModified, chunkCount, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsFatalException(ex))
                    {
                        Interlocked.Increment(ref filesFailed);
                        var fatal = new PipelineError(PipelineErrorSeverity.Fatal, filePath, "Processing", ex.Message, ex);
                        errors.Add(fatal);
                        Interlocked.CompareExchange(ref fatalError, fatal, null);
                        _logger.LogCritical(ex, "Fatal error re-indexing file: {FilePath}. Halting pipeline.", filePath);
                        linkedCts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref filesFailed);
                        _logger.LogError(ex, "Error re-indexing file: {FilePath}", filePath);
                        errors.Add(new PipelineError(PipelineErrorSeverity.Error, filePath, "Processing", ex.Message, ex));
                        CheckForServiceLevelFailure(ref fatalError, errors, filesNewlyIndexed + filesReIndexed, filesFailed, linkedCts);
                    }
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Pipeline halted during modified file processing due to fatal error.");
            }
        }

        // Delete chunks for removed files in parallel (if not halted)
        if (fatalError == null)
        {
            try
            {
                await Parallel.ForEachAsync(plan.DeletedFiles, parallelOptions, async (filePath, token) =>
                {
                    try
                    {
                        await _vectorStore.DeleteBySourceFileAsync(filePath, token);
                        await _incrementalIndexingService.RemoveStateAsync(filePath, token);
                        Interlocked.Increment(ref filesDeleted);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsFatalException(ex))
                    {
                        Interlocked.Increment(ref filesFailed);
                        var fatal = new PipelineError(PipelineErrorSeverity.Fatal, filePath, "Deletion", ex.Message, ex);
                        errors.Add(fatal);
                        Interlocked.CompareExchange(ref fatalError, fatal, null);
                        _logger.LogCritical(ex, "Fatal error deleting chunks for file: {FilePath}. Halting pipeline.", filePath);
                        linkedCts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref filesFailed);
                        _logger.LogError(ex, "Error deleting chunks for removed file: {FilePath}", filePath);
                        errors.Add(new PipelineError(PipelineErrorSeverity.Error, filePath, "Deletion", ex.Message, ex));
                    }
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Pipeline halted during deletion processing due to fatal error.");
            }
        }

        int filesSkipped = plan.UnchangedFiles.Count;

        stopwatch.Stop();
        int totalFilesConsidered = filesNewlyIndexed + filesReIndexed + filesDeleted + filesSkipped + filesFailed;

        return new PipelineResult(
            TotalFilesProcessed: totalFilesConsidered,
            TotalChunksGenerated: totalChunks,
            TotalErrors: errors.Count,
            ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
            FilesNewlyIndexed: filesNewlyIndexed,
            FilesReIndexed: filesReIndexed,
            FilesDeleted: filesDeleted,
            FilesSkipped: filesSkipped,
            FilesFailed: filesFailed);
    }

    /// <summary>
    /// Processes a single file through the pipeline stages: parse → chunk → metadata → embed → store.
    /// Returns the number of chunks generated.
    /// </summary>
    private async Task<int> ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        IReadOnlyList<Chunk> chunks;

        if (SupportedDocumentExtensions.Contains(extension))
        {
            chunks = await ProcessDocumentFileAsync(filePath, extension, cancellationToken);
        }
        else if (SupportedCodeExtensions.Contains(extension))
        {
            chunks = await ProcessCodeFileAsync(filePath, extension, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Unsupported file extension: {FilePath}", filePath);
            return 0;
        }

        if (chunks.Count == 0)
        {
            _logger.LogDebug("No chunks generated for file: {FilePath}", filePath);
            return 0;
        }

        // Embed: Generate embeddings for all chunks
        var chunkTexts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embeddingGenerator.GenerateEmbeddingsBatchAsync(chunkTexts, cancellationToken);

        // Store: Create VectorDocument objects and upsert
        var vectorDocuments = new List<VectorDocument>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = embeddings[i];
            var id = GenerateDocumentId(filePath, chunk.Index);

            vectorDocuments.Add(new VectorDocument(
                Id: id,
                Embedding: embedding,
                ChunkText: chunk.Text,
                Metadata: chunk.Metadata));
        }

        await _vectorStore.UpsertAsync(vectorDocuments, cancellationToken);

        _logger.LogDebug("Processed {ChunkCount} chunks for file: {FilePath}", chunks.Count, filePath);
        return chunks.Count;
    }

    /// <summary>
    /// Processes a document file (.md, .pdf, .docx) through parsing, chunking, and metadata enrichment.
    /// </summary>
    private async Task<IReadOnlyList<Chunk>> ProcessDocumentFileAsync(string filePath, string extension, CancellationToken cancellationToken)
    {
        // Find appropriate parser
        var parser = _documentParsers.FirstOrDefault(p => p.CanParse(extension));
        if (parser == null)
        {
            _logger.LogWarning("No document parser found for extension: {Extension}", extension);
            return Array.Empty<Chunk>();
        }

        // Parse
        var parsedDocument = await parser.ParseAsync(filePath, cancellationToken);

        // Chunk
        var chunks = _chunkingService.ChunkDocument(parsedDocument);

        // Metadata: Enrich each chunk with metadata
        var headings = MetadataExtractor.ResolveHeadingsForChunks(parsedDocument, chunks);
        var enrichedChunks = new List<Chunk>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var enrichedMetadata = _metadataExtractor.ExtractDocumentMetadata(parsedDocument, chunks[i].Index, headings[i]);
            enrichedChunks.Add(new Chunk(
                Text: chunks[i].Text,
                Index: chunks[i].Index,
                ContextHeader: chunks[i].ContextHeader,
                Metadata: enrichedMetadata));
        }

        return enrichedChunks;
    }

    /// <summary>
    /// Processes a code file (.cs, .jsx, .tsx, .sql) through parsing, chunking, and metadata enrichment.
    /// </summary>
    private async Task<IReadOnlyList<Chunk>> ProcessCodeFileAsync(string filePath, string extension, CancellationToken cancellationToken)
    {
        // Find appropriate parser
        var parser = _codeParsers.FirstOrDefault(p => p.CanParse(extension));
        if (parser == null)
        {
            _logger.LogWarning("No code parser found for extension: {Extension}", extension);
            return Array.Empty<Chunk>();
        }

        // Parse
        var parsedCode = await parser.ParseAsync(filePath, cancellationToken);

        // Chunk
        var chunks = _chunkingService.ChunkCode(parsedCode);

        // Metadata: Enrich each chunk with metadata
        var enrichedChunks = new List<Chunk>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var containingSymbol = MetadataExtractor.ResolveContainingSymbol(parsedCode, chunks[i].Index);
            var enrichedMetadata = _metadataExtractor.ExtractCodeMetadata(parsedCode, chunks[i].Index, containingSymbol);
            enrichedChunks.Add(new Chunk(
                Text: chunks[i].Text,
                Index: chunks[i].Index,
                ContextHeader: chunks[i].ContextHeader,
                Metadata: enrichedMetadata));
        }

        return enrichedChunks;
    }

    /// <summary>
    /// Enumerates all supported files (both document and code) in the target path.
    /// </summary>
    private IReadOnlyList<string> EnumerateAllSupportedFiles(string targetPath)
    {
        var allFiles = new List<string>();
        var allExtensions = SupportedDocumentExtensions.Union(SupportedCodeExtensions).ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            var files = Directory.EnumerateFiles(targetPath, "*.*", SearchOption.AllDirectories)
                .Where(f => allExtensions.Contains(Path.GetExtension(f)));
            allFiles.AddRange(files);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied enumerating files in: {TargetPath}", targetPath);
            throw;
        }

        return allFiles;
    }

    /// <summary>
    /// Generates a deterministic document ID: Base64Url(SHA256(sourceFilePath + "|" + chunkIndex)).
    /// </summary>
    private static string GenerateDocumentId(string sourceFilePath, int chunkIndex)
    {
        var input = $"{sourceFilePath}|{chunkIndex}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Determines whether an exception represents a fatal error that should halt the pipeline.
    /// Fatal errors include: target path inaccessible (UnauthorizedAccessException, DirectoryNotFoundException).
    /// </summary>
    private static bool IsFatalException(Exception ex)
    {
        return FatalPathExceptionTypes.Contains(ex.GetType())
            || (ex.InnerException != null && FatalPathExceptionTypes.Contains(ex.InnerException.GetType()));
    }

    /// <summary>
    /// Checks whether all files processed so far have failed, indicating a service-level failure
    /// (e.g., Azure services unavailable). If so, escalates to a fatal error and halts.
    /// Requires at least 3 consecutive failures with zero successes to trigger.
    /// </summary>
    private void CheckForServiceLevelFailure(
        ref PipelineError? fatalError,
        ConcurrentBag<PipelineError> errors,
        int successCount,
        int failedCount,
        CancellationTokenSource linkedCts)
    {
        // If all files in this batch have failed (at least 3 failures, zero successes),
        // it's likely a service-level failure
        if (successCount == 0 && failedCount >= 3 && fatalError == null)
        {
            var serviceFatal = new PipelineError(
                PipelineErrorSeverity.Fatal,
                string.Empty,
                "Processing",
                $"Service-level failure detected: all {failedCount} files failed. Azure services may be unavailable.",
                null);
            if (Interlocked.CompareExchange(ref fatalError, serviceFatal, null) == null)
            {
                errors.Add(serviceFatal);
                _logger.LogCritical(
                    "Service-level failure detected after {FailedCount} consecutive failures with zero successes. Halting pipeline.",
                    failedCount);
                linkedCts.Cancel();
            }
        }
    }
}
