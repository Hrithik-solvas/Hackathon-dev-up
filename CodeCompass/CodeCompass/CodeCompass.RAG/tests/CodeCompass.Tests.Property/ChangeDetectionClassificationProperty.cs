using CodeCompass.Indexing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 21: Change detection classification correctness.
/// For any set of current files and stored state, classification is correct
/// (new/modified/deleted/unchanged) and the union of all categories equals the
/// union of files on disk and in state.
///
/// **Validates: Requirements 7.2**
/// </summary>
public class ChangeDetectionClassificationProperty : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private static readonly string[] SupportedExtensions = { ".cs", ".ts", ".md", ".sql", ".tsx", ".jsx", ".js" };

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    /// <summary>
    /// Creates a test scenario from seed values: files on disk, files in state,
    /// with correct classification expectations.
    /// </summary>
    private (string targetDir, string stateFilePath, IncrementalIndexingService service,
        HashSet<string> expectedDiskNormalized, HashSet<string> stateKeys)
        SetupScenario(int diskCountSeed, int stateOnlySeed, int unchangedSeed, int modifiedSeed)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"codecompass_prop_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirs.Add(tempDir);

        var targetDir = Path.Combine(tempDir, "repo");
        Directory.CreateDirectory(targetDir);

        var stateFilePath = Path.Combine(tempDir, ".codecompass-state.json");

        // Determine file counts
        var diskFileCount = (diskCountSeed % 8) + 1; // 1-8 files on disk
        var stateOnlyCount = (stateOnlySeed % 4); // 0-3 deleted files (state-only)
        var unchangedCount = Math.Min((unchangedSeed % (diskFileCount + 1)), diskFileCount); // 0 to diskFileCount
        var modifiedCount = Math.Min((modifiedSeed % (diskFileCount - unchangedCount + 1)), diskFileCount - unchangedCount);

        var state = new IndexingState
        {
            Version = 1,
            LastRunTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Files = new Dictionary<string, FileState>()
        };

        var expectedDiskNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Create files on disk
        for (var i = 0; i < diskFileCount; i++)
        {
            var ext = SupportedExtensions[i % SupportedExtensions.Length];
            var fileName = $"file_{i}{ext}";
            var filePath = Path.Combine(targetDir, fileName);
            File.WriteAllText(filePath, $"// content {i}");
            expectedDiskNormalized.Add(IncrementalIndexingService.NormalizePath(filePath));

            var normalizedPath = IncrementalIndexingService.NormalizePath(filePath);

            if (i < unchangedCount)
            {
                // Unchanged: state has same timestamp as file
                var actualTimestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
                state.Files[normalizedPath] = new FileState
                {
                    LastModified = actualTimestamp,
                    ChunkCount = 2
                };
            }
            else if (i < unchangedCount + modifiedCount)
            {
                // Modified: state has different timestamp
                state.Files[normalizedPath] = new FileState
                {
                    LastModified = DateTimeOffset.UtcNow.AddDays(-30),
                    ChunkCount = 3
                };
            }
            // else: new file (not in state)
        }

        // Add state-only entries (deleted files)
        var stateKeys = new HashSet<string>(state.Files.Keys, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < stateOnlyCount; i++)
        {
            var deletedPath = $"/deleted/old_file_{i}.cs";
            state.Files[deletedPath] = new FileState
            {
                LastModified = DateTimeOffset.UtcNow.AddDays(-10),
                ChunkCount = 1
            };
            stateKeys.Add(deletedPath);
        }

        // Save state to disk
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(stateFilePath, json);

        var logger = NullLogger<IncrementalIndexingService>.Instance;
        var service = new IncrementalIndexingService(logger);
        service.SetStateFilePath(stateFilePath);

        return (targetDir, stateFilePath, service, expectedDiskNormalized, stateKeys);
    }

    [Property(MaxTest = 50)]
    public async void UnionOfCategories_EqualsUnionOfDiskAndState(
        PositiveInt diskSeed, PositiveInt stateSeed, PositiveInt unchangedSeed, PositiveInt modifiedSeed)
    {
        // Arrange
        var (targetDir, _, service, expectedDiskNormalized, stateKeys) =
            SetupScenario(diskSeed.Get, stateSeed.Get, unchangedSeed.Get, modifiedSeed.Get);

        // Act
        var plan = await service.ComputePlanAsync(targetDir);

        // Collect all categorized files (normalized)
        var allCategorized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in plan.NewFiles) allCategorized.Add(IncrementalIndexingService.NormalizePath(f));
        foreach (var f in plan.ModifiedFiles) allCategorized.Add(IncrementalIndexingService.NormalizePath(f));
        foreach (var f in plan.DeletedFiles) allCategorized.Add(IncrementalIndexingService.NormalizePath(f));
        foreach (var f in plan.UnchangedFiles) allCategorized.Add(IncrementalIndexingService.NormalizePath(f));

        // Expected: union of disk files and state files
        var allExpected = new HashSet<string>(expectedDiskNormalized, StringComparer.OrdinalIgnoreCase);
        foreach (var key in stateKeys)
        {
            allExpected.Add(key);
        }

        // Assert
        allCategorized.Should().BeEquivalentTo(allExpected,
            "union of all categories should equal union of disk files and state files");
    }

    [Property(MaxTest = 50)]
    public async void NewFiles_AreNotInState(
        PositiveInt diskSeed, PositiveInt stateSeed, PositiveInt unchangedSeed, PositiveInt modifiedSeed)
    {
        // Arrange
        var (targetDir, _, service, _, stateKeys) =
            SetupScenario(diskSeed.Get, stateSeed.Get, unchangedSeed.Get, modifiedSeed.Get);

        // Act
        var plan = await service.ComputePlanAsync(targetDir);

        // Assert: new files should not exist in stored state
        foreach (var newFile in plan.NewFiles)
        {
            var normalized = IncrementalIndexingService.NormalizePath(newFile);
            stateKeys.Should().NotContain(normalized,
                $"new file '{normalized}' should not be in stored state");
        }
    }

    [Property(MaxTest = 50)]
    public async void DeletedFiles_AreNotOnDisk(
        PositiveInt diskSeed, PositiveInt stateSeed, PositiveInt unchangedSeed, PositiveInt modifiedSeed)
    {
        // Arrange
        var (targetDir, _, service, expectedDiskNormalized, _) =
            SetupScenario(diskSeed.Get, stateSeed.Get, unchangedSeed.Get, modifiedSeed.Get);

        // Act
        var plan = await service.ComputePlanAsync(targetDir);

        // Assert: deleted files should not exist on disk
        foreach (var deletedFile in plan.DeletedFiles)
        {
            var normalized = IncrementalIndexingService.NormalizePath(deletedFile);
            expectedDiskNormalized.Should().NotContain(normalized,
                $"deleted file '{normalized}' should not exist on disk");
        }
    }

    [Property(MaxTest = 50)]
    public async void CategoriesAreDisjoint(
        PositiveInt diskSeed, PositiveInt stateSeed, PositiveInt unchangedSeed, PositiveInt modifiedSeed)
    {
        // Arrange
        var (targetDir, _, service, _, _) =
            SetupScenario(diskSeed.Get, stateSeed.Get, unchangedSeed.Get, modifiedSeed.Get);

        // Act
        var plan = await service.ComputePlanAsync(targetDir);

        // Normalize all categories
        var newNorm = plan.NewFiles.Select(IncrementalIndexingService.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modNorm = plan.ModifiedFiles.Select(IncrementalIndexingService.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var delNorm = plan.DeletedFiles.Select(IncrementalIndexingService.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unchNorm = plan.UnchangedFiles.Select(IncrementalIndexingService.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert: categories should be mutually exclusive
        newNorm.Intersect(modNorm).Should().BeEmpty("new and modified should be disjoint");
        newNorm.Intersect(delNorm).Should().BeEmpty("new and deleted should be disjoint");
        newNorm.Intersect(unchNorm).Should().BeEmpty("new and unchanged should be disjoint");
        modNorm.Intersect(delNorm).Should().BeEmpty("modified and deleted should be disjoint");
        modNorm.Intersect(unchNorm).Should().BeEmpty("modified and unchanged should be disjoint");
        delNorm.Intersect(unchNorm).Should().BeEmpty("deleted and unchanged should be disjoint");
    }
}
