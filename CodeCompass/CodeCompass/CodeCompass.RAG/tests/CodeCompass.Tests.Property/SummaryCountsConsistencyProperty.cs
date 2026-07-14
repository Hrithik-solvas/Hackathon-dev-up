using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Pipeline;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 23: Summary counts consistency.
/// FilesNewlyIndexed + FilesReIndexed + FilesDeleted + FilesSkipped + FilesFailed = TotalFilesProcessed,
/// all counts are non-negative, and ElapsedMilliseconds >= 0.
///
/// **Validates: Requirements 8.1**
/// </summary>
public class SummaryCountsConsistencyProperty : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private static readonly string[] SupportedExtensions = { ".md", ".pdf", ".docx", ".cs", ".jsx", ".tsx", ".sql" };

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { /* best effort */ }
        }
    }

    private (PipelineOrchestrator orchestrator, string testDir, ConfigurableIncrementalService incrementalService, HashSet<string> failFiles)
        CreateOrchestrator()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"PipelineProp23_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        _tempDirs.Add(testDir);

        var failFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var docParser = new SelectiveFailDocParser(failFiles);
        var codeParser = new SelectiveFailCodeParser(failFiles);
        var chunkingService = new SimpleChunkingService();
        var metadataExtractor = new SimpleMetadataExtractor();
        var embeddingGenerator = new SimpleEmbeddingGenerator();
        var vectorStore = new SimpleVectorStore();
        var incrementalService = new ConfigurableIncrementalService();
        var repositoryEnumerator = new SimpleRepositoryEnumerator();

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 1, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var logger = NullLogger<PipelineOrchestrator>.Instance;

        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { docParser },
            new ICodeParser[] { codeParser },
            chunkingService,
            metadataExtractor,
            embeddingGenerator,
            vectorStore,
            incrementalService,
            repositoryEnumerator,
            logger,
            settings);

        return (orchestrator, testDir, incrementalService, failFiles);
    }

    [Property(MaxTest = 50)]
    public async void FullMode_SumOfCountsEqualsTotalProcessed(PositiveInt fileSeed, PositiveInt failSeed)
    {
        // Arrange: we limit failures to 0-2 and ensure there are always some successes
        // to avoid triggering the pipeline's service-level failure detection (halts at 3+ failures with 0 successes)
        var successCount = (fileSeed.Get % 5) + 1; // 1-5 succeeding files
        var failCount = failSeed.Get % 3; // 0-2 failing files

        var (orchestrator, testDir, _, failFiles) = CreateOrchestrator();

        // Create succeeding files (alphabetically first to process before failures)
        for (int i = 0; i < successCount; i++)
        {
            var ext = SupportedExtensions[i % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"a_good_{i}{ext}");
            File.WriteAllText(filePath, $"content {i}");
        }

        // Create failing files (alphabetically later)
        for (int i = 0; i < failCount; i++)
        {
            var ext = SupportedExtensions[(i + successCount) % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"z_bad_{i}{ext}");
            File.WriteAllText(filePath, $"content {i}");
            failFiles.Add(filePath);
        }

        var request = new PipelineRequest(testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: sum of counts equals total
        var sum = result.FilesNewlyIndexed + result.FilesReIndexed + result.FilesDeleted + result.FilesSkipped + result.FilesFailed;
        sum.Should().Be(result.TotalFilesProcessed,
            "newly indexed + re-indexed + deleted + skipped + failed should equal total files processed");

        // Assert: all counts non-negative
        result.FilesNewlyIndexed.Should().BeGreaterThanOrEqualTo(0);
        result.FilesReIndexed.Should().BeGreaterThanOrEqualTo(0);
        result.FilesDeleted.Should().BeGreaterThanOrEqualTo(0);
        result.FilesSkipped.Should().BeGreaterThanOrEqualTo(0);
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(0);
        result.TotalFilesProcessed.Should().BeGreaterThanOrEqualTo(0);
        result.TotalChunksGenerated.Should().BeGreaterThanOrEqualTo(0);
        result.TotalErrors.Should().BeGreaterThanOrEqualTo(0);

        // Assert: elapsed time >= 0
        result.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Property(MaxTest = 50)]
    public async void IncrementalMode_SumOfCountsEqualsTotalProcessed(
        PositiveInt newSeed, PositiveInt modSeed, PositiveInt delSeed, PositiveInt unchangedSeed, PositiveInt failSeed)
    {
        // Arrange: to avoid service-level failure detection (halts at 3+ consecutive failures with 0 successes),
        // we limit failures to at most 2 and ensure there's at least 1 success among new/modified files.
        var newCount = (newSeed.Get % 3) + 1; // 1-3 new files (ensures at least 1 success)
        var modCount = modSeed.Get % 3; // 0-2 modified
        var delCount = delSeed.Get % 3; // 0-2 deleted
        var unchangedCount = unchangedSeed.Get % 4; // 0-3 unchanged

        var (orchestrator, testDir, incrementalService, failFiles) = CreateOrchestrator();

        var newFiles = new List<string>();
        var modifiedFiles = new List<string>();

        // Create new files (alphabetically first to succeed before failures)
        for (int i = 0; i < newCount; i++)
        {
            var ext = SupportedExtensions[i % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"a_new_{i}{ext}");
            File.WriteAllText(filePath, $"new content {i}");
            newFiles.Add(filePath);
        }

        // Create modified files
        for (int i = 0; i < modCount; i++)
        {
            var ext = SupportedExtensions[(i + 3) % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"b_mod_{i}{ext}");
            File.WriteAllText(filePath, $"modified content {i}");
            modifiedFiles.Add(filePath);
        }

        // Configure at most 2 files to fail, and only from the end of the list
        var allProcessableFiles = newFiles.Concat(modifiedFiles).ToList();
        var maxFail = Math.Min(failSeed.Get % 3, allProcessableFiles.Count - 1); // 0-2 and always leave at least 1 success
        maxFail = Math.Max(maxFail, 0);
        for (int i = allProcessableFiles.Count - maxFail; i < allProcessableFiles.Count; i++)
        {
            failFiles.Add(allProcessableFiles[i]);
        }

        // Set up deleted and unchanged files
        var deletedFiles = Enumerable.Range(0, delCount).Select(i => $"/deleted/file_{i}.md").ToList();
        var unchangedFiles = Enumerable.Range(0, unchangedCount).Select(i => $"/unchanged/file_{i}.cs").ToList();

        incrementalService.PlanToReturn = new IndexingPlan(
            newFiles,
            modifiedFiles,
            deletedFiles,
            unchangedFiles);

        var request = new PipelineRequest(testDir, IndexingMode.Incremental);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: sum of counts equals total
        var sum = result.FilesNewlyIndexed + result.FilesReIndexed + result.FilesDeleted + result.FilesSkipped + result.FilesFailed;
        sum.Should().Be(result.TotalFilesProcessed,
            "newly indexed + re-indexed + deleted + skipped + failed should equal total files processed");

        // Assert: all counts non-negative
        result.FilesNewlyIndexed.Should().BeGreaterThanOrEqualTo(0);
        result.FilesReIndexed.Should().BeGreaterThanOrEqualTo(0);
        result.FilesDeleted.Should().BeGreaterThanOrEqualTo(0);
        result.FilesSkipped.Should().BeGreaterThanOrEqualTo(0);
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(0);
        result.TotalFilesProcessed.Should().BeGreaterThanOrEqualTo(0);
        result.TotalChunksGenerated.Should().BeGreaterThanOrEqualTo(0);
        result.TotalErrors.Should().BeGreaterThanOrEqualTo(0);

        // Assert: elapsed time >= 0
        result.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    #region Fakes

    private class SelectiveFailDocParser : IDocumentParser
    {
        private readonly HashSet<string> _failFiles;

        public SelectiveFailDocParser(HashSet<string> failFiles)
        {
            _failFiles = failFiles;
        }

        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_failFiles.Contains(filePath))
                throw new InvalidOperationException($"Simulated failure for {filePath}");

            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedDocument("content", new List<Heading>(), metadata));
        }
    }

    private class SelectiveFailCodeParser : ICodeParser
    {
        private readonly HashSet<string> _failFiles;

        public SelectiveFailCodeParser(HashSet<string> failFiles)
        {
            _failFiles = failFiles;
        }

        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_failFiles.Contains(filePath))
                throw new InvalidOperationException($"Simulated failure for {filePath}");

            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedCode("code", new List<CodeSymbol>(), new List<string>(), metadata));
        }
    }

    private class SimpleChunkingService : IChunkingService
    {
        public IReadOnlyList<Chunk> ChunkDocument(ParsedDocument document, ChunkingOptions? options = null)
        {
            var metadata = new ChunkMetadata(document.SourceMetadata.FilePath, 0, "document", null, document.SourceMetadata.LastModified, null);
            return new[] { new Chunk("chunk", 0, null, metadata) };
        }

        public IReadOnlyList<Chunk> ChunkCode(ParsedCode code, ChunkingOptions? options = null)
        {
            var metadata = new ChunkMetadata(code.SourceMetadata.FilePath, 0, "code", "csharp", code.SourceMetadata.LastModified, null);
            return new[] { new Chunk("chunk", 0, null, metadata) };
        }
    }

    private class SimpleMetadataExtractor : IMetadataExtractor
    {
        public ChunkMetadata ExtractDocumentMetadata(ParsedDocument document, int chunkIndex, string? nearestHeading)
            => new(document.SourceMetadata.FilePath, chunkIndex, "document", null, document.SourceMetadata.LastModified, nearestHeading);

        public ChunkMetadata ExtractCodeMetadata(ParsedCode code, int chunkIndex, string? containingSymbol)
            => new(code.SourceMetadata.FilePath, chunkIndex, "code", "csharp", code.SourceMetadata.LastModified, containingSymbol);
    }

    private class SimpleEmbeddingGenerator : IEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(new float[1536]);

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[1536]).ToList());
    }

    private class SimpleVectorStore : IVectorStore
    {
        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteBySourceFileAsync(string sourceFilePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class ConfigurableIncrementalService : IIncrementalIndexingService
    {
        public IndexingPlan? PlanToReturn { get; set; }

        public Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default)
            => Task.FromResult(PlanToReturn ?? new IndexingPlan(
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>()));

        public Task UpdateStateAsync(string filePath, DateTimeOffset lastModified, int chunkCount = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveStateAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class SimpleRepositoryEnumerator : IRepositoryEnumerator
    {
        public IReadOnlyList<string> EnumerateFiles(string directoryPath)
            => Array.Empty<string>();
    }

    #endregion
}
