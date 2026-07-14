using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Unit.Pipeline;

public class PipelineOrchestratorTests : IDisposable
{
    private readonly string _testDir;
    private readonly FakeDocumentParser _documentParser;
    private readonly FakeCodeParser _codeParser;
    private readonly FakeChunkingService _chunkingService;
    private readonly FakeMetadataExtractor _metadataExtractor;
    private readonly FakeEmbeddingGenerator _embeddingGenerator;
    private readonly FakeVectorStore _vectorStore;
    private readonly FakeIncrementalIndexingService _incrementalIndexingService;
    private readonly FakeRepositoryEnumerator _repositoryEnumerator;
    private readonly PipelineOrchestrator _orchestrator;

    public PipelineOrchestratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PipelineOrchestratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _documentParser = new FakeDocumentParser();
        _codeParser = new FakeCodeParser();
        _chunkingService = new FakeChunkingService();
        _metadataExtractor = new FakeMetadataExtractor();
        _embeddingGenerator = new FakeEmbeddingGenerator();
        _vectorStore = new FakeVectorStore();
        _incrementalIndexingService = new FakeIncrementalIndexingService();
        _repositoryEnumerator = new FakeRepositoryEnumerator();

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var logger = NullLogger<PipelineOrchestrator>.Instance;

        _orchestrator = new PipelineOrchestrator(
            new[] { _documentParser },
            new[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            logger,
            settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Validation Tests

    [Fact]
    public async Task RunAsync_NullTargetPath_ThrowsArgumentException()
    {
        var request = new PipelineRequest("", IndexingMode.Full);

        var act = () => _orchestrator.RunAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_NonExistentPath_ThrowsDirectoryNotFoundException()
    {
        var request = new PipelineRequest("/non/existent/path", IndexingMode.Full);

        var act = () => _orchestrator.RunAsync(request);

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    #endregion

    #region Full Mode Tests

    [Fact]
    public async Task RunAsync_FullMode_EmptyDirectory_ReturnsZeroCounts()
    {
        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var result = await _orchestrator.RunAsync(request);

        result.TotalFilesProcessed.Should().Be(0);
        result.TotalChunksGenerated.Should().Be(0);
        result.TotalErrors.Should().Be(0);
        result.FilesNewlyIndexed.Should().Be(0);
        result.FilesFailed.Should().Be(0);
        result.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_FullMode_WithMarkdownFile_ProcessesThroughAllStages()
    {
        // Arrange: create a .md file in the test directory
        var mdFile = Path.Combine(_testDir, "readme.md");
        await File.WriteAllTextAsync(mdFile, "# Hello\n\nWorld");

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await _orchestrator.RunAsync(request);

        // Assert
        result.TotalFilesProcessed.Should().Be(1);
        result.TotalChunksGenerated.Should().Be(1);
        result.FilesNewlyIndexed.Should().Be(1);
        result.FilesFailed.Should().Be(0);

        // Verify all stages were called
        _documentParser.ParseCalled.Should().BeTrue();
        _chunkingService.ChunkDocumentCalled.Should().BeTrue();
        _metadataExtractor.ExtractDocumentMetadataCalled.Should().BeTrue();
        _embeddingGenerator.GenerateBatchCalled.Should().BeTrue();
        _vectorStore.UpsertCalled.Should().BeTrue();
        _incrementalIndexingService.UpdateStateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_FullMode_WithCodeFile_ProcessesThroughAllStages()
    {
        // Arrange: create a .cs file in the test directory
        var csFile = Path.Combine(_testDir, "Service.cs");
        await File.WriteAllTextAsync(csFile, "public class Service {}");

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await _orchestrator.RunAsync(request);

        // Assert
        result.TotalFilesProcessed.Should().Be(1);
        result.TotalChunksGenerated.Should().Be(1);
        result.FilesNewlyIndexed.Should().Be(1);
        result.FilesFailed.Should().Be(0);

        _codeParser.ParseCalled.Should().BeTrue();
        _chunkingService.ChunkCodeCalled.Should().BeTrue();
        _metadataExtractor.ExtractCodeMetadataCalled.Should().BeTrue();
        _embeddingGenerator.GenerateBatchCalled.Should().BeTrue();
        _vectorStore.UpsertCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_FullMode_UnsupportedFileExtension_IsIgnored()
    {
        // Arrange: create only a .txt file
        var txtFile = Path.Combine(_testDir, "notes.txt");
        await File.WriteAllTextAsync(txtFile, "Just notes");

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var result = await _orchestrator.RunAsync(request);

        result.TotalFilesProcessed.Should().Be(0);
        result.TotalChunksGenerated.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_FullMode_MultipleFiles_ProcessesAll()
    {
        // Arrange: create multiple supported files
        await File.WriteAllTextAsync(Path.Combine(_testDir, "doc.md"), "# Doc");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "code.cs"), "class A {}");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "query.sql"), "SELECT 1");

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var result = await _orchestrator.RunAsync(request);

        result.TotalFilesProcessed.Should().Be(3);
        result.TotalChunksGenerated.Should().Be(3);
        result.FilesNewlyIndexed.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_FullMode_FileFailure_ContinuesProcessing()
    {
        // Arrange: create two files, make the parser fail on one
        await File.WriteAllTextAsync(Path.Combine(_testDir, "good.md"), "# Good");
        var badFile = Path.Combine(_testDir, "bad.md");
        await File.WriteAllTextAsync(badFile, "# Bad");

        _documentParser.FailOnFile = badFile;

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var result = await _orchestrator.RunAsync(request);

        // One succeeds, one fails
        result.FilesNewlyIndexed.Should().Be(1);
        result.FilesFailed.Should().Be(1);
        result.TotalErrors.Should().Be(1);
        result.TotalFilesProcessed.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_FullMode_ElapsedTimeIsPositive()
    {
        await File.WriteAllTextAsync(Path.Combine(_testDir, "test.md"), "# Test");
        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var result = await _orchestrator.RunAsync(request);

        result.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Incremental Mode Tests

    [Fact]
    public async Task RunAsync_IncrementalMode_ProcessesNewFiles()
    {
        // Arrange
        var newFile = Path.Combine(_testDir, "new.md");
        await File.WriteAllTextAsync(newFile, "# New");

        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: new[] { newFile },
            ModifiedFiles: Array.Empty<string>(),
            DeletedFiles: Array.Empty<string>(),
            UnchangedFiles: Array.Empty<string>());

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        result.FilesNewlyIndexed.Should().Be(1);
        result.FilesReIndexed.Should().Be(0);
        result.FilesDeleted.Should().Be(0);
        result.FilesSkipped.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_ReIndexesModifiedFiles()
    {
        // Arrange
        var modifiedFile = Path.Combine(_testDir, "modified.cs");
        await File.WriteAllTextAsync(modifiedFile, "class Modified {}");

        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: Array.Empty<string>(),
            ModifiedFiles: new[] { modifiedFile },
            DeletedFiles: Array.Empty<string>(),
            UnchangedFiles: Array.Empty<string>());

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        result.FilesReIndexed.Should().Be(1);
        // Should delete old chunks before re-indexing
        _vectorStore.DeleteBySourceFileCalled.Should().BeTrue();
        _vectorStore.LastDeletedFilePath.Should().Be(modifiedFile);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_DeletesChunksForRemovedFiles()
    {
        // Arrange
        var deletedFile = "/some/deleted/file.md";
        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: Array.Empty<string>(),
            ModifiedFiles: Array.Empty<string>(),
            DeletedFiles: new[] { deletedFile },
            UnchangedFiles: Array.Empty<string>());

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        result.FilesDeleted.Should().Be(1);
        _vectorStore.DeleteBySourceFileCalled.Should().BeTrue();
        _incrementalIndexingService.RemoveStateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_SkipsUnchangedFiles()
    {
        // Arrange
        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: Array.Empty<string>(),
            ModifiedFiles: Array.Empty<string>(),
            DeletedFiles: Array.Empty<string>(),
            UnchangedFiles: new[] { "/repo/unchanged.md", "/repo/unchanged2.cs" });

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        result.FilesSkipped.Should().Be(2);
        result.FilesNewlyIndexed.Should().Be(0);
        result.FilesReIndexed.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_SummaryCountsAreConsistent()
    {
        // Arrange: mix of new, modified, deleted, and unchanged files
        var newFile = Path.Combine(_testDir, "new.md");
        var modifiedFile = Path.Combine(_testDir, "modified.cs");
        await File.WriteAllTextAsync(newFile, "# New");
        await File.WriteAllTextAsync(modifiedFile, "class Mod {}");

        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: new[] { newFile },
            ModifiedFiles: new[] { modifiedFile },
            DeletedFiles: new[] { "/deleted/file.md" },
            UnchangedFiles: new[] { "/unchanged.md" });

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        // Total = new + reindexed + deleted + skipped + failed
        var expectedTotal = result.FilesNewlyIndexed + result.FilesReIndexed + result.FilesDeleted + result.FilesSkipped + result.FilesFailed;
        result.TotalFilesProcessed.Should().Be(expectedTotal);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_FailedFile_DoesNotUpdateState()
    {
        // Arrange
        var failFile = Path.Combine(_testDir, "fail.md");
        await File.WriteAllTextAsync(failFile, "# Fail");

        _documentParser.FailOnFile = failFile;

        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: new[] { failFile },
            ModifiedFiles: Array.Empty<string>(),
            DeletedFiles: Array.Empty<string>(),
            UnchangedFiles: Array.Empty<string>());

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        var result = await _orchestrator.RunAsync(request);

        result.FilesFailed.Should().Be(1);
        result.FilesNewlyIndexed.Should().Be(0);
        // State should NOT have been updated for the failed file
        _incrementalIndexingService.UpdatedFiles.Should().NotContain(failFile);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        await File.WriteAllTextAsync(Path.Combine(_testDir, "test.md"), "# Test");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        var act = () => _orchestrator.RunAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Parallel Processing Tests (Task 10.2)

    [Fact]
    public async Task RunAsync_FullMode_ProcessesMultipleFilesInParallel()
    {
        // Arrange: create several files to process
        for (int i = 0; i < 8; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDir, $"file{i}.md"), $"# File {i}");
        }

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await _orchestrator.RunAsync(request);

        // Assert: all files processed
        result.TotalFilesProcessed.Should().Be(8);
        result.FilesNewlyIndexed.Should().Be(8);
        result.FilesFailed.Should().Be(0);
        result.TotalChunksGenerated.Should().Be(8);
    }

    [Fact]
    public async Task RunAsync_FullMode_RespectsConcurrencyLevel()
    {
        // Arrange: create a tracking parser that records concurrent executions
        var concurrencyTracker = new ConcurrencyTrackingDocumentParser();
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 2, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { concurrencyTracker },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        for (int i = 0; i < 10; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDir, $"file{i}.md"), $"# File {i}");
        }

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: all processed and max concurrent never exceeded configured level
        result.TotalFilesProcessed.Should().Be(10);
        concurrencyTracker.MaxConcurrentCalls.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task RunAsync_ConcurrencyLevel_ClampedToMinimum()
    {
        // Arrange: ConcurrencyLevel below minimum (0 → clamped to 1)
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 0, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { _documentParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        await File.WriteAllTextAsync(Path.Combine(_testDir, "test.md"), "# Test");
        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act - should not throw; processes with concurrency 1
        var result = await orchestrator.RunAsync(request);

        result.TotalFilesProcessed.Should().Be(1);
        result.FilesNewlyIndexed.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_ConcurrencyLevel_ClampedToMaximum()
    {
        // Arrange: ConcurrencyLevel above maximum (100 → clamped to 32)
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 100, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { _documentParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        await File.WriteAllTextAsync(Path.Combine(_testDir, "test.md"), "# Test");
        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act - should not throw; processes with concurrency 32
        var result = await orchestrator.RunAsync(request);

        result.TotalFilesProcessed.Should().Be(1);
        result.FilesNewlyIndexed.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_ProcessesNewFilesInParallel()
    {
        // Arrange: multiple new files
        var files = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(_testDir, $"new{i}.md");
            await File.WriteAllTextAsync(file, $"# New {i}");
            files.Add(file);
        }

        _incrementalIndexingService.PlanToReturn = new IndexingPlan(
            NewFiles: files,
            ModifiedFiles: Array.Empty<string>(),
            DeletedFiles: Array.Empty<string>(),
            UnchangedFiles: Array.Empty<string>());

        var request = new PipelineRequest(_testDir, IndexingMode.Incremental);

        // Act
        var result = await _orchestrator.RunAsync(request);

        // Assert
        result.FilesNewlyIndexed.Should().Be(5);
        result.FilesFailed.Should().Be(0);
    }

    #endregion

    #region Error Isolation and Fatal Error Tests (Task 10.3)

    [Fact]
    public async Task RunAsync_FullMode_IndividualFileFailure_DoesNotHaltPipeline()
    {
        // Arrange: create multiple files, fail on one specific file
        await File.WriteAllTextAsync(Path.Combine(_testDir, "good1.md"), "# Good 1");
        var badFile = Path.Combine(_testDir, "bad.md");
        await File.WriteAllTextAsync(badFile, "# Bad");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "good2.md"), "# Good 2");

        _documentParser.FailOnFile = badFile;

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await _orchestrator.RunAsync(request);

        // Assert: other files were still processed
        result.FilesNewlyIndexed.Should().Be(2);
        result.FilesFailed.Should().Be(1);
        result.TotalFilesProcessed.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_FullMode_FatalError_UnauthorizedAccess_HaltsPipeline()
    {
        // Arrange: set up parser to throw UnauthorizedAccessException
        var fatalParser = new FatalExceptionDocumentParser(
            new UnauthorizedAccessException("Access denied to file"));

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 1, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { fatalParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        // Create multiple files - with concurrency 1, the fatal error on first should halt
        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDir, $"file{i}.md"), $"# File {i}");
        }

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: pipeline halted, not all files processed
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(1);
        result.TotalFilesProcessed.Should().BeLessThan(5);
        result.TotalErrors.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RunAsync_FullMode_FatalError_DirectoryNotFound_HaltsPipeline()
    {
        // Arrange: set up parser to throw DirectoryNotFoundException
        var fatalParser = new FatalExceptionDocumentParser(
            new DirectoryNotFoundException("Directory not found"));

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 1, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { fatalParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDir, $"file{i}.md"), $"# File {i}");
        }

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: pipeline halted with partial results
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(1);
        result.TotalFilesProcessed.Should().BeLessThan(5);
    }

    [Fact]
    public async Task RunAsync_FullMode_ServiceLevelFailure_AllFilesFail_HaltsPipeline()
    {
        // Arrange: parser fails on ALL files (simulating Azure service unavailable)
        var alwaysFailParser = new AlwaysFailDocumentParser();

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 1, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { alwaysFailParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDir, $"file{i}.md"), $"# File {i}");
        }

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: pipeline detected service-level failure and halted
        result.FilesNewlyIndexed.Should().Be(0);
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(3); // At least 3 failures to trigger halt
        result.TotalFilesProcessed.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task RunAsync_FullMode_FatalError_ReturnsPartialSummary()
    {
        // Arrange: first file succeeds, second throws fatal error
        var selectiveFatalParser = new SelectiveFatalDocumentParser();

        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 1, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        var orchestrator = new PipelineOrchestrator(
            new IDocumentParser[] { selectiveFatalParser },
            new ICodeParser[] { _codeParser },
            _chunkingService,
            _metadataExtractor,
            _embeddingGenerator,
            _vectorStore,
            _incrementalIndexingService,
            _repositoryEnumerator,
            NullLogger<PipelineOrchestrator>.Instance,
            settings);

        // Create files with predictable sort order
        await File.WriteAllTextAsync(Path.Combine(_testDir, "a_good.md"), "# Good");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "b_fatal.md"), "# Fatal");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "c_unreached.md"), "# Unreached");

        selectiveFatalParser.FatalFileSuffix = "b_fatal.md";

        var request = new PipelineRequest(_testDir, IndexingMode.Full);

        // Act
        var result = await orchestrator.RunAsync(request);

        // Assert: partial results returned
        result.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        result.FilesNewlyIndexed.Should().BeGreaterThanOrEqualTo(1); // at least 1 succeeded
        result.FilesFailed.Should().BeGreaterThanOrEqualTo(1); // at least the fatal one
        result.TotalErrors.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Fakes

    private class FakeDocumentParser : IDocumentParser
    {
        public bool ParseCalled { get; private set; }
        public string? FailOnFile { get; set; }

        public bool CanParse(string fileExtension)
        {
            return fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (FailOnFile != null && filePath == FailOnFile)
            {
                throw new InvalidOperationException($"Simulated parse failure for {filePath}");
            }

            ParseCalled = true;
            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedDocument("Parsed content", new List<Heading>(), metadata));
        }
    }

    private class FakeCodeParser : ICodeParser
    {
        public bool ParseCalled { get; private set; }

        public bool CanParse(string fileExtension)
        {
            return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase)
                || fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
                || fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ParseCalled = true;
            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedCode("Code content", new List<CodeSymbol>(), new List<string>(), metadata));
        }
    }

    private class FakeChunkingService : IChunkingService
    {
        public bool ChunkDocumentCalled { get; private set; }
        public bool ChunkCodeCalled { get; private set; }

        public IReadOnlyList<Chunk> ChunkDocument(ParsedDocument document, ChunkingOptions? options = null)
        {
            ChunkDocumentCalled = true;
            var metadata = new ChunkMetadata(document.SourceMetadata.FilePath, 0, "document", null, document.SourceMetadata.LastModified, null);
            return new[] { new Chunk("chunk text", 0, null, metadata) };
        }

        public IReadOnlyList<Chunk> ChunkCode(ParsedCode code, ChunkingOptions? options = null)
        {
            ChunkCodeCalled = true;
            var metadata = new ChunkMetadata(code.SourceMetadata.FilePath, 0, "code", "csharp", code.SourceMetadata.LastModified, null);
            return new[] { new Chunk("code chunk", 0, null, metadata) };
        }
    }

    private class FakeMetadataExtractor : IMetadataExtractor
    {
        public bool ExtractDocumentMetadataCalled { get; private set; }
        public bool ExtractCodeMetadataCalled { get; private set; }

        public ChunkMetadata ExtractDocumentMetadata(ParsedDocument document, int chunkIndex, string? nearestHeading)
        {
            ExtractDocumentMetadataCalled = true;
            return new ChunkMetadata(document.SourceMetadata.FilePath, chunkIndex, "document", null, document.SourceMetadata.LastModified, nearestHeading);
        }

        public ChunkMetadata ExtractCodeMetadata(ParsedCode code, int chunkIndex, string? containingSymbol)
        {
            ExtractCodeMetadataCalled = true;
            return new ChunkMetadata(code.SourceMetadata.FilePath, chunkIndex, "code", "csharp", code.SourceMetadata.LastModified, containingSymbol);
        }
    }

    private class FakeEmbeddingGenerator : IEmbeddingGenerator
    {
        public bool GenerateBatchCalled { get; private set; }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new float[1536]);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            GenerateBatchCalled = true;
            var embeddings = texts.Select(_ => new float[1536]).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
        }
    }

    private class FakeVectorStore : IVectorStore
    {
        public bool UpsertCalled { get; private set; }
        public bool DeleteBySourceFileCalled { get; private set; }
        public string? LastDeletedFilePath { get; private set; }
        public List<VectorDocument> UpsertedDocuments { get; } = new();

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
        {
            UpsertCalled = true;
            UpsertedDocuments.AddRange(documents);
            return Task.CompletedTask;
        }

        public Task DeleteBySourceFileAsync(string sourceFilePath, CancellationToken cancellationToken = default)
        {
            DeleteBySourceFileCalled = true;
            LastDeletedFilePath = sourceFilePath;
            return Task.CompletedTask;
        }
    }

    private class FakeIncrementalIndexingService : IIncrementalIndexingService
    {
        public bool UpdateStateCalled { get; private set; }
        public bool RemoveStateCalled { get; private set; }
        public List<string> UpdatedFiles { get; } = new();
        public IndexingPlan? PlanToReturn { get; set; }

        public Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanToReturn ?? new IndexingPlan(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
        }

        public Task UpdateStateAsync(string filePath, DateTimeOffset lastModified, int chunkCount = 0, CancellationToken cancellationToken = default)
        {
            UpdateStateCalled = true;
            UpdatedFiles.Add(filePath);
            return Task.CompletedTask;
        }

        public Task RemoveStateAsync(string filePath, CancellationToken cancellationToken = default)
        {
            RemoveStateCalled = true;
            return Task.CompletedTask;
        }
    }

    private class FakeRepositoryEnumerator : IRepositoryEnumerator
    {
        public IReadOnlyList<string> EnumerateFiles(string directoryPath)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Tracks max concurrency during parsing to verify parallelism settings.
    /// </summary>
    private class ConcurrencyTrackingDocumentParser : IDocumentParser
    {
        private int _currentConcurrency;
        private int _maxConcurrency;

        public int MaxConcurrentCalls => _maxConcurrency;

        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            // Update max using a spin-compare pattern
            int max;
            do
            {
                max = _maxConcurrency;
            } while (current > max && Interlocked.CompareExchange(ref _maxConcurrency, current, max) != max);

            await Task.Delay(50, cancellationToken); // Simulate work

            Interlocked.Decrement(ref _currentConcurrency);

            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return new ParsedDocument("Parsed content", new List<Heading>(), metadata);
        }
    }

    /// <summary>
    /// Always throws a fatal exception.
    /// </summary>
    private class FatalExceptionDocumentParser : IDocumentParser
    {
        private readonly Exception _exceptionToThrow;

        public FatalExceptionDocumentParser(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            throw _exceptionToThrow;
        }
    }

    /// <summary>
    /// Always throws a non-fatal exception.
    /// </summary>
    private class AlwaysFailDocumentParser : IDocumentParser
    {
        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"Simulated service failure for {filePath}");
        }
    }

    /// <summary>
    /// Throws a fatal exception only for files matching a suffix.
    /// </summary>
    private class SelectiveFatalDocumentParser : IDocumentParser
    {
        public string? FatalFileSuffix { get; set; }

        public bool CanParse(string fileExtension) =>
            fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (FatalFileSuffix != null && filePath.EndsWith(FatalFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied to {filePath}");
            }

            var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), DateTimeOffset.UtcNow);
            return Task.FromResult(new ParsedDocument("Parsed content", new List<Heading>(), metadata));
        }
    }

    #endregion
}
