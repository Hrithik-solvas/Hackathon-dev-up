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
/// Property 24: Full mode processes all supported files.
/// For any directory tree and full indexing request, every file with a supported extension
/// is processed and no unsupported-extension files are processed.
///
/// **Validates: Requirements 8.4**
/// </summary>
public class FullModeProcessesAllSupportedFilesProperty : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private static readonly string[] SupportedExtensions = { ".md", ".pdf", ".docx", ".cs", ".jsx", ".tsx", ".sql" };
    private static readonly string[] UnsupportedExtensions = { ".txt", ".json", ".xml", ".yaml", ".png", ".jpg", ".exe", ".dll", ".log", ".csv" };

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { /* best effort */ }
        }
    }

    private (PipelineOrchestrator orchestrator, string testDir, TrackingVectorStore vectorStore)
        CreateOrchestrator()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"PipelineProp24_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        _tempDirs.Add(testDir);

        var docParser = new SimpleDocumentParser();
        var codeParser = new SimpleCodeParser();
        var chunkingService = new SimpleChunkingService();
        var metadataExtractor = new SimpleMetadataExtractor();
        var embeddingGenerator = new SimpleEmbeddingGenerator();
        var vectorStore = new TrackingVectorStore();
        var incrementalService = new SimpleIncrementalService();
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

        return (orchestrator, testDir, vectorStore);
    }

    [Property(MaxTest = 50)]
    public async void AllSupportedFilesAreProcessed_NoUnsupportedFilesProcessed(
        PositiveInt supportedSeed, PositiveInt unsupportedSeed, PositiveInt depthSeed)
    {
        // Arrange: create a mix of supported and unsupported files in a directory tree
        var supportedCount = (supportedSeed.Get % 5) + 1; // 1-5 supported files
        var unsupportedCount = unsupportedSeed.Get % 5; // 0-4 unsupported files
        var depth = (depthSeed.Get % 3); // 0-2 levels of subdirectories

        var (orchestrator, testDir, vectorStore) = CreateOrchestrator();

        var supportedFiles = new List<string>();
        var unsupportedFiles = new List<string>();

        // Create supported files
        for (int i = 0; i < supportedCount; i++)
        {
            var ext = SupportedExtensions[i % SupportedExtensions.Length];
            var subDir = testDir;
            if (depth > 0 && i % 2 == 0)
            {
                subDir = Path.Combine(testDir, $"sub_{i % (depth + 1)}");
                Directory.CreateDirectory(subDir);
            }
            var filePath = Path.Combine(subDir, $"supported_{i}{ext}");
            File.WriteAllText(filePath, $"content {i}");
            supportedFiles.Add(filePath);
        }

        // Create unsupported files
        for (int i = 0; i < unsupportedCount; i++)
        {
            var ext = UnsupportedExtensions[i % UnsupportedExtensions.Length];
            var subDir = testDir;
            if (depth > 0 && i % 2 == 1)
            {
                subDir = Path.Combine(testDir, $"sub_{i % (depth + 1)}");
                Directory.CreateDirectory(subDir);
            }
            var filePath = Path.Combine(subDir, $"unsupported_{i}{ext}");
            File.WriteAllText(filePath, $"unsupported content {i}");
            unsupportedFiles.Add(filePath);
        }

        var request = new PipelineRequest(testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: every supported file was processed
        result.TotalFilesProcessed.Should().Be(supportedCount,
            $"all {supportedCount} supported files should be processed");

        // Assert: the result processes exactly the supported file count
        result.FilesNewlyIndexed.Should().Be(supportedCount,
            "all supported files should be newly indexed in full mode");

        // Assert: vector store received chunks only for supported files
        var processedPaths = vectorStore.UpsertedSourcePaths;
        foreach (var supportedFile in supportedFiles)
        {
            processedPaths.Should().Contain(supportedFile,
                $"supported file {Path.GetFileName(supportedFile)} should have been processed");
        }

        foreach (var unsupportedFile in unsupportedFiles)
        {
            processedPaths.Should().NotContain(unsupportedFile,
                $"unsupported file {Path.GetFileName(unsupportedFile)} should NOT have been processed");
        }
    }

    [Property(MaxTest = 30)]
    public async void FullMode_WithOnlyUnsupportedFiles_ProcessesZero(PositiveInt countSeed)
    {
        // Arrange: directory with only unsupported files
        var count = (countSeed.Get % 5) + 1; // 1-5 unsupported files

        var (orchestrator, testDir, vectorStore) = CreateOrchestrator();

        for (int i = 0; i < count; i++)
        {
            var ext = UnsupportedExtensions[i % UnsupportedExtensions.Length];
            var filePath = Path.Combine(testDir, $"file_{i}{ext}");
            File.WriteAllText(filePath, $"content {i}");
        }

        var request = new PipelineRequest(testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: no files processed
        result.TotalFilesProcessed.Should().Be(0);
        result.FilesNewlyIndexed.Should().Be(0);
        result.TotalChunksGenerated.Should().Be(0);
        vectorStore.UpsertedSourcePaths.Should().BeEmpty();
    }

    [Property(MaxTest = 30)]
    public async void FullMode_NestedDirectories_FindsAllSupportedFiles(PositiveInt fileSeed, PositiveInt nestSeed)
    {
        // Arrange: create deeply nested directories with supported files
        var fileCount = (fileSeed.Get % 4) + 1; // 1-4 files
        var nestDepth = (nestSeed.Get % 3) + 1; // 1-3 levels deep

        var (orchestrator, testDir, _) = CreateOrchestrator();

        var currentDir = testDir;
        var createdFiles = 0;

        for (int level = 0; level <= nestDepth && createdFiles < fileCount; level++)
        {
            if (level > 0)
            {
                currentDir = Path.Combine(currentDir, $"level_{level}");
                Directory.CreateDirectory(currentDir);
            }

            var ext = SupportedExtensions[createdFiles % SupportedExtensions.Length];
            var filePath = Path.Combine(currentDir, $"nested_{createdFiles}{ext}");
            File.WriteAllText(filePath, $"nested content {createdFiles}");
            createdFiles++;
        }

        var request = new PipelineRequest(testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: all nested supported files found and processed
        result.TotalFilesProcessed.Should().Be(createdFiles,
            "all supported files in nested directories should be processed");
        result.FilesNewlyIndexed.Should().Be(createdFiles);
    }

    #region Fakes

    private class SimpleDocumentParser : IDocumentParser
    {
        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedDocument("content", new List<Heading>(), metadata));
        }
    }

    private class SimpleCodeParser : ICodeParser
    {
        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
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

    private class TrackingVectorStore : IVectorStore
    {
        private readonly List<string> _upsertedSourcePaths = new();
        private readonly object _lock = new();

        public IReadOnlyList<string> UpsertedSourcePaths
        {
            get { lock (_lock) return _upsertedSourcePaths.ToList(); }
        }

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                foreach (var doc in documents)
                {
                    _upsertedSourcePaths.Add(doc.Metadata.SourceFilePath);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteBySourceFileAsync(string sourceFilePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class SimpleIncrementalService : IIncrementalIndexingService
    {
        public Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new IndexingPlan(
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
