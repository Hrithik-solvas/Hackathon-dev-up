using System.Text.Json;
using CodeCompass.Core.Models;
using CodeCompass.Indexing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Pipeline;

public class IncrementalIndexingServiceTests : IDisposable
{
    private readonly IncrementalIndexingService _service;
    private readonly string _tempDir;
    private readonly string _stateFilePath;

    public IncrementalIndexingServiceTests()
    {
        var logger = NullLogger<IncrementalIndexingService>.Instance;
        _service = new IncrementalIndexingService(logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"codecompass_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _stateFilePath = Path.Combine(_tempDir, ".codecompass-state.json");
        _service.SetStateFilePath(_stateFilePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    #region Task 9.1: State Serialization / Deserialization

    [Fact]
    public async Task SaveStateAsync_CreatesJsonFile()
    {
        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.Parse("2024-01-15T10:30:00Z"),
            Files = new Dictionary<string, FileState>
            {
                ["/path/to/file.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-15T09:00:00Z"),
                    ChunkCount = 5
                }
            }
        };

        await _service.SaveStateAsync(_stateFilePath, state);

        File.Exists(_stateFilePath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(_stateFilePath);
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"lastRunTimestamp\"");
        json.Should().Contain("\"files\"");
    }

    [Fact]
    public async Task LoadStateAsync_ReturnsEmptyState_WhenFileDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.json");

        var state = await _service.LoadStateAsync(nonExistentPath);

        state.Should().NotBeNull();
        state.Version.Should().Be(1);
        state.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task StateRoundTrip_PreservesAllData()
    {
        var originalState = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.Parse("2024-01-15T10:30:00Z"),
            Files = new Dictionary<string, FileState>
            {
                ["/path/to/file1.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-15T09:00:00Z"),
                    ChunkCount = 5
                },
                ["/path/to/file2.md"] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-14T08:00:00Z"),
                    ChunkCount = 3
                }
            }
        };

        await _service.SaveStateAsync(_stateFilePath, originalState);
        var loadedState = await _service.LoadStateAsync(_stateFilePath);

        loadedState.Version.Should().Be(originalState.Version);
        loadedState.LastRunTimestamp.Should().Be(originalState.LastRunTimestamp);
        loadedState.Files.Should().HaveCount(2);
        loadedState.Files["/path/to/file1.cs"].LastModified.Should().Be(originalState.Files["/path/to/file1.cs"].LastModified);
        loadedState.Files["/path/to/file1.cs"].ChunkCount.Should().Be(5);
        loadedState.Files["/path/to/file2.md"].LastModified.Should().Be(originalState.Files["/path/to/file2.md"].LastModified);
        loadedState.Files["/path/to/file2.md"].ChunkCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadStateAsync_ReturnsEmptyState_WhenFileIsCorrupted()
    {
        await File.WriteAllTextAsync(_stateFilePath, "{ this is not valid json }}}");

        var state = await _service.LoadStateAsync(_stateFilePath);

        state.Should().NotBeNull();
        state.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveStateAsync_CreatesDirectoryIfNotExists()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "dir", "state.json");

        var state = new IndexingState { Version = 1 };
        await _service.SaveStateAsync(nestedPath, state);

        File.Exists(nestedPath).Should().BeTrue();
    }

    #endregion

    #region Task 9.2: ComputePlanAsync

    [Fact]
    public async Task ComputePlanAsync_AllFilesNew_WhenNoStateFileExists()
    {
        // Create some files in the target directory
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "file1.cs"), "// code");
        File.WriteAllText(Path.Combine(targetDir, "file2.cs"), "// code");
        File.WriteAllText(Path.Combine(targetDir, "readme.md"), "# Hello");

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.NewFiles.Should().HaveCount(3);
        plan.ModifiedFiles.Should().BeEmpty();
        plan.DeletedFiles.Should().BeEmpty();
        plan.UnchangedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputePlanAsync_ClassifiesModifiedFiles()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        var filePath = Path.Combine(targetDir, "file1.cs");
        File.WriteAllText(filePath, "// code");

        // Save state with a different timestamp
        var normalizedPath = IncrementalIndexingService.NormalizePath(filePath);
        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Files = new Dictionary<string, FileState>
            {
                [normalizedPath] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow.AddDays(-2), // Different from actual file timestamp
                    ChunkCount = 3
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, state);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.ModifiedFiles.Should().HaveCount(1);
        plan.ModifiedFiles[0].Should().Be(filePath);
    }

    [Fact]
    public async Task ComputePlanAsync_ClassifiesUnchangedFiles()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        var filePath = Path.Combine(targetDir, "file1.cs");
        File.WriteAllText(filePath, "// code");

        // Get the actual file timestamp and save state with the same timestamp
        var actualTimestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
        var normalizedPath = IncrementalIndexingService.NormalizePath(filePath);
        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, FileState>
            {
                [normalizedPath] = new FileState
                {
                    LastModified = actualTimestamp,
                    ChunkCount = 3
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, state);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.UnchangedFiles.Should().HaveCount(1);
        plan.UnchangedFiles[0].Should().Be(filePath);
        plan.NewFiles.Should().BeEmpty();
        plan.ModifiedFiles.Should().BeEmpty();
        plan.DeletedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputePlanAsync_ClassifiesDeletedFiles()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);

        // State references a file that no longer exists on disk
        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, FileState>
            {
                ["/some/deleted/file.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow.AddDays(-1),
                    ChunkCount = 2
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, state);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.DeletedFiles.Should().HaveCount(1);
        plan.DeletedFiles[0].Should().Be("/some/deleted/file.cs");
    }

    [Fact]
    public async Task ComputePlanAsync_MixedClassification()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);

        // Create a new file (not in state)
        var newFilePath = Path.Combine(targetDir, "new_file.cs");
        File.WriteAllText(newFilePath, "// new");

        // Create an unchanged file
        var unchangedFilePath = Path.Combine(targetDir, "unchanged.cs");
        File.WriteAllText(unchangedFilePath, "// unchanged");
        var unchangedTimestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(unchangedFilePath), TimeSpan.Zero);

        // Create a modified file
        var modifiedFilePath = Path.Combine(targetDir, "modified.cs");
        File.WriteAllText(modifiedFilePath, "// modified");

        var normalizedUnchanged = IncrementalIndexingService.NormalizePath(unchangedFilePath);
        var normalizedModified = IncrementalIndexingService.NormalizePath(modifiedFilePath);

        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, FileState>
            {
                [normalizedUnchanged] = new FileState
                {
                    LastModified = unchangedTimestamp,
                    ChunkCount = 2
                },
                [normalizedModified] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow.AddDays(-5), // Different from actual
                    ChunkCount = 4
                },
                ["/deleted/old_file.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow.AddDays(-10),
                    ChunkCount = 1
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, state);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.NewFiles.Should().HaveCount(1);
        plan.ModifiedFiles.Should().HaveCount(1);
        plan.UnchangedFiles.Should().HaveCount(1);
        plan.DeletedFiles.Should().HaveCount(1);
    }

    [Fact]
    public async Task ComputePlanAsync_EmptyDirectory_ReturnsEmptyPlan()
    {
        var targetDir = Path.Combine(_tempDir, "empty_repo");
        Directory.CreateDirectory(targetDir);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.NewFiles.Should().BeEmpty();
        plan.ModifiedFiles.Should().BeEmpty();
        plan.DeletedFiles.Should().BeEmpty();
        plan.UnchangedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputePlanAsync_EmptyDirectory_WithStateFile_AllDeleted()
    {
        var targetDir = Path.Combine(_tempDir, "empty_repo");
        Directory.CreateDirectory(targetDir);

        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, FileState>
            {
                ["/old/file1.cs"] = new FileState { LastModified = DateTimeOffset.UtcNow.AddDays(-1), ChunkCount = 1 },
                ["/old/file2.md"] = new FileState { LastModified = DateTimeOffset.UtcNow.AddDays(-2), ChunkCount = 2 }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, state);

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.NewFiles.Should().BeEmpty();
        plan.ModifiedFiles.Should().BeEmpty();
        plan.DeletedFiles.Should().HaveCount(2);
        plan.UnchangedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputePlanAsync_IgnoresUnsupportedFileExtensions()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "data.json"), "{}");
        File.WriteAllText(Path.Combine(targetDir, "image.png"), "binary");
        File.WriteAllText(Path.Combine(targetDir, "valid.cs"), "// code");

        var plan = await _service.ComputePlanAsync(targetDir);

        plan.NewFiles.Should().HaveCount(1);
        plan.NewFiles[0].Should().EndWith("valid.cs");
    }

    [Fact]
    public async Task ComputePlanAsync_ThrowsForNullOrEmptyPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputePlanAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputePlanAsync(null!));
    }

    [Fact]
    public async Task ComputePlanAsync_SupportsCancellation()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "file1.cs"), "// code");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.ComputePlanAsync(targetDir, cts.Token));
    }

    #endregion

    #region Task 9.3: UpdateStateAsync and RemoveStateAsync

    [Fact]
    public async Task UpdateStateAsync_AddsNewFileToState()
    {
        var filePath = "/path/to/newfile.cs";
        var lastModified = DateTimeOffset.Parse("2024-01-20T10:00:00Z");
        var chunkCount = 7;

        await _service.UpdateStateAsync(filePath, lastModified, chunkCount);

        var state = await _service.LoadStateAsync(_stateFilePath);
        var normalizedPath = IncrementalIndexingService.NormalizePath(filePath);
        state.Files.Should().ContainKey(normalizedPath);
        state.Files[normalizedPath].LastModified.Should().Be(lastModified);
        state.Files[normalizedPath].ChunkCount.Should().Be(chunkCount);
    }

    [Fact]
    public async Task UpdateStateAsync_UpdatesExistingFileInState()
    {
        // Pre-populate state
        var filePath = "/path/to/existing.cs";
        var initialState = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Files = new Dictionary<string, FileState>
            {
                [filePath] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-10T08:00:00Z"),
                    ChunkCount = 3
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, initialState);

        var newLastModified = DateTimeOffset.Parse("2024-01-20T12:00:00Z");
        var newChunkCount = 5;

        await _service.UpdateStateAsync(filePath, newLastModified, newChunkCount);

        var state = await _service.LoadStateAsync(_stateFilePath);
        state.Files[filePath].LastModified.Should().Be(newLastModified);
        state.Files[filePath].ChunkCount.Should().Be(newChunkCount);
    }

    [Fact]
    public async Task UpdateStateAsync_PreservesOtherFilesInState()
    {
        var existingPath = "/path/to/existing.cs";
        var initialState = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Files = new Dictionary<string, FileState>
            {
                [existingPath] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-10T08:00:00Z"),
                    ChunkCount = 3
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, initialState);

        var newFilePath = "/path/to/new.cs";
        await _service.UpdateStateAsync(newFilePath, DateTimeOffset.UtcNow, 2);

        var state = await _service.LoadStateAsync(_stateFilePath);
        state.Files.Should().HaveCount(2);
        state.Files.Should().ContainKey(existingPath);
        state.Files.Should().ContainKey(newFilePath);
    }

    [Fact]
    public async Task RemoveStateAsync_RemovesFileFromState()
    {
        var filePath = "/path/to/delete.cs";
        var initialState = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Files = new Dictionary<string, FileState>
            {
                [filePath] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-15T09:00:00Z"),
                    ChunkCount = 4
                },
                ["/path/to/keep.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.Parse("2024-01-14T09:00:00Z"),
                    ChunkCount = 2
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, initialState);

        await _service.RemoveStateAsync(filePath);

        var state = await _service.LoadStateAsync(_stateFilePath);
        state.Files.Should().NotContainKey(filePath);
        state.Files.Should().ContainKey("/path/to/keep.cs");
    }

    [Fact]
    public async Task RemoveStateAsync_NoOpForNonExistentFile()
    {
        var initialState = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, FileState>
            {
                ["/path/to/existing.cs"] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow,
                    ChunkCount = 1
                }
            }
        };
        await _service.SaveStateAsync(_stateFilePath, initialState);

        // Should not throw
        await _service.RemoveStateAsync("/nonexistent/file.cs");

        var state = await _service.LoadStateAsync(_stateFilePath);
        state.Files.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateStateAsync_ThrowsForNullOrEmptyPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStateAsync("", DateTimeOffset.UtcNow, 1));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStateAsync(null!, DateTimeOffset.UtcNow, 1));
    }

    [Fact]
    public async Task RemoveStateAsync_ThrowsForNullOrEmptyPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RemoveStateAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RemoveStateAsync(null!));
    }

    #endregion

    #region Task 9.4: Corrupted/Missing State File Handling

    [Fact]
    public async Task ComputePlanAsync_AllFilesNew_WhenStateFileIsCorrupted()
    {
        var targetDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "file1.cs"), "// code");
        File.WriteAllText(Path.Combine(targetDir, "file2.md"), "# Hello");

        // Write corrupted state file
        await File.WriteAllTextAsync(_stateFilePath, "{ this is not valid json }}}");

        var plan = await _service.ComputePlanAsync(targetDir);

        // All files should be classified as new (full re-index)
        plan.NewFiles.Should().HaveCount(2);
        plan.ModifiedFiles.Should().BeEmpty();
        plan.DeletedFiles.Should().BeEmpty();
        plan.UnchangedFiles.Should().BeEmpty();
    }

    #endregion
}
