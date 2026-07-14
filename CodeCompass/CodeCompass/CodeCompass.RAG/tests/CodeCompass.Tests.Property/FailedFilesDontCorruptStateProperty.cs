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
/// Property 22: Failed files don't corrupt state.
/// For any batch where some files fail, the pipeline continues, does not update state
/// for failed files, and produces results for non-failed files.
///
/// **Validates: Requirements 8.3**
/// </summary>
public class FailedFilesDontCorruptStateProperty : IDisposable
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

    private (PipelineOrchestrator orchestrator, string testDir, TrackingIncrementalService incrementalService, SelectiveFailDocumentParser docParser)
        CreateOrchestrator(HashSet<string> failFiles)
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"PipelineProp22_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        _tempDirs.Add(testDir);

        var docParser = new SelectiveFailDocumentParser(failFiles);
        var codeParser = new SelectiveFailCodeParser(failFiles);
        var chunkingService = new SimpleChunkingService();
        var metadataExtractor = new SimpleMetadataExtractor();
        var embeddingGenerator = new SimpleEmbeddingGenerator();
        var vectorStore = new SimpleVectorStore();
        var incrementalService = new TrackingIncrementalService();
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

        return (orchestrator, testDir, incrementalService, docParser);
    }

    [Property(MaxTest = 50)]
    public async void PipelineContinuesAfterFailures_AndDoesNotUpdateStateForFailedFiles(
        PositiveInt totalSeed, PositiveInt failSeed)
    {
        // Arrange: create a mix of files, with some configured to fail.
        // The pipeline halts after 3 consecutive failures with zero successes (service-level failure detection).
        // To test the "continue after failure" behavior properly, we limit fail count to 1-2
        // and ensure there are always some succeeding files.
        var successCount = (totalSeed.Get % 4) + 2; // 2-5 succeeding files
        var failCount = (failSeed.Get % 2) + 1; // 1-2 failing files

        var failFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (orchestrator, testDir, incrementalService, _) = CreateOrchestrator(failFiles);

        var allFiles = new List<string>();

        // Create succeeding files first (alphabetically earlier so they process first)
        for (int i = 0; i < successCount; i++)
        {
            var ext = SupportedExtensions[i % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"a_good_{i}{ext}");
            File.WriteAllText(filePath, $"good content {i}");
            allFiles.Add(filePath);
        }

        // Create failing files (alphabetically later)
        for (int i = 0; i < failCount; i++)
        {
            var ext = SupportedExtensions[(i + successCount) % SupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"z_bad_{i}{ext}");
            File.WriteAllText(filePath, $"bad content {i}");
            allFiles.Add(filePath);
            failFiles.Add(filePath);
        }

        var totalFiles = successCount + failCount;
        var request = new PipelineRequest(testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: pipeline doesn't throw (continues after failures)
        // Assert: result shows both succeeded and failed counts
        result.FilesFailed.Should().Be(failCount,
            "all configured-to-fail files should be counted as failed");
        result.FilesNewlyIndexed.Should().Be(successCount,
            "non-failed files should be counted as newly indexed");
        result.TotalFilesProcessed.Should().Be(totalFiles,
            "total files processed should include both succeeded and failed");

        // Assert: state was NOT updated for failed files
        foreach (var failedFile in failFiles)
        {
            incrementalService.UpdatedFiles.Should().NotContain(failedFile,
                $"state should not be updated for failed file: {failedFile}");
        }

        // Assert: state WAS updated for succeeded files
        var expectedSucceeded = allFiles.Where(f => !failFiles.Contains(f)).ToList();
        foreach (var succeededFile in expectedSucceeded)
        {
            incrementalService.UpdatedFiles.Should().Contain(succeededFile,
                $"state should be updated for succeeded file: {succeededFile}");
        }
    }

    [Property(MaxTest = 30)]
    public async void PipelineResultContainsCountsForBothSucceededAndFailed(
        PositiveInt totalSeed, PositiveInt failSeed)
    {
        // Arrange: limit failures to 1-2 to avoid triggering service-level failure detection
        var successCount = (totalSeed.Get % 4) + 2; // 2-5 files succeeding
        var failCount = (failSeed.Get % 2) + 1; // 1-2 failing

        var failFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (orchestrator, testDir, _, _) = CreateOrchestrator(failFiles);

        // Create succeeding files (alphabetically first)
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

        // Assert: result accurately reflects both categories
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(1);
        result.FilesNewlyIndexed.Should().BeGreaterThanOrEqualTo(1);
        (result.FilesNewlyIndexed + result.FilesFailed).Should().Be(result.TotalFilesProcessed,
            "in full mode, newly indexed + failed should equal total processed");
        result.TotalErrors.Should().BeGreaterThanOrEqualTo(result.FilesFailed,
            "error count should be at least as many as failed files");
    }

    #region Fakes

    private class SelectiveFailDocumentParser : IDocumentParser
    {
        private readonly HashSet<string> _failFiles;

        public SelectiveFailDocumentParser(HashSet<string> failFiles)
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

    private class TrackingIncrementalService : IIncrementalIndexingService
    {
        public List<string> UpdatedFiles { get; } = new();

        public Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new IndexingPlan(
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>()));

        public Task UpdateStateAsync(string filePath, DateTimeOffset lastModified, int chunkCount = 0, CancellationToken cancellationToken = default)
        {
            UpdatedFiles.Add(filePath);
            return Task.CompletedTask;
        }

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
