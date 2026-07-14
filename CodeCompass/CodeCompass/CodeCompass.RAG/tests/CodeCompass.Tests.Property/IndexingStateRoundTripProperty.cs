using System.Text.Json;
using CodeCompass.Indexing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 20: Indexing state round-trip serialization.
/// Serializing and deserializing any valid indexing state record produces an equivalent record
/// with identical paths, timestamps, and chunk counts.
///
/// **Validates: Requirements 7.1**
/// </summary>
public class IndexingStateRoundTripProperty
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] PathSegments = { "src", "lib", "docs", "tests", "utils", "core", "api" };
    private static readonly string[] FileNames = { "file.cs", "main.ts", "readme.md", "query.sql", "component.tsx", "utils.js" };

    /// <summary>
    /// Generates a deterministic IndexingState from seed values.
    /// </summary>
    private static IndexingState CreateState(int versionSeed, int timestampDaysSeed, int fileCountSeed, int chunkSeed)
    {
        var version = (versionSeed % 5) + 1;
        var timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z").AddDays(timestampDaysSeed % 365);
        var fileCount = (fileCountSeed % 10) + 1;
        var rng = new Random(chunkSeed);

        var files = new Dictionary<string, FileState>();
        for (var i = 0; i < fileCount; i++)
        {
            var segment = PathSegments[i % PathSegments.Length];
            var fileName = FileNames[i % FileNames.Length];
            var path = $"/{segment}/project/{fileName}_{i}";
            files[path] = new FileState
            {
                LastModified = timestamp.AddHours(-(i + 1)),
                ChunkCount = rng.Next(0, 100)
            };
        }

        return new IndexingState
        {
            Version = version,
            LastRunTimestamp = timestamp,
            Files = files
        };
    }

    [Property(MaxTest = 100)]
    public void SerializeDeserialize_ProducesEquivalentState(
        PositiveInt versionSeed, PositiveInt timestampSeed, PositiveInt fileCountSeed, PositiveInt chunkSeed)
    {
        // Arrange
        var original = CreateState(versionSeed.Get, timestampSeed.Get, fileCountSeed.Get, chunkSeed.Get);

        // Act: serialize to JSON
        var json = JsonSerializer.Serialize(original, JsonOptions);

        // Act: deserialize back
        var deserialized = JsonSerializer.Deserialize<IndexingState>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Version.Should().Be(original.Version, "Version should survive round-trip");
        deserialized.LastRunTimestamp.Should().Be(original.LastRunTimestamp, "LastRunTimestamp should survive round-trip");
        deserialized.Files.Should().HaveCount(original.Files.Count, "File count should survive round-trip");

        foreach (var (path, expectedState) in original.Files)
        {
            deserialized.Files.Should().ContainKey(path, $"File path '{path}' should be preserved");
            deserialized.Files[path].LastModified.Should().Be(expectedState.LastModified,
                $"LastModified for '{path}' should survive round-trip");
            deserialized.Files[path].ChunkCount.Should().Be(expectedState.ChunkCount,
                $"ChunkCount for '{path}' should survive round-trip");
        }
    }

    [Property(MaxTest = 100)]
    public void SerializeDeserialize_JsonContainsAllExpectedFields(
        PositiveInt versionSeed, PositiveInt timestampSeed, PositiveInt fileCountSeed, PositiveInt chunkSeed)
    {
        // Arrange
        var original = CreateState(versionSeed.Get, timestampSeed.Get, fileCountSeed.Get, chunkSeed.Get);

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);

        // Assert: JSON should contain the expected field names
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"lastRunTimestamp\"");
        json.Should().Contain("\"files\"");
        json.Should().Contain("\"lastModified\"");
        json.Should().Contain("\"chunkCount\"");
    }

    [Property(MaxTest = 100)]
    public void DoubleRoundTrip_ProducesSameJson(
        PositiveInt versionSeed, PositiveInt timestampSeed, PositiveInt fileCountSeed, PositiveInt chunkSeed)
    {
        // Arrange
        var original = CreateState(versionSeed.Get, timestampSeed.Get, fileCountSeed.Get, chunkSeed.Get);

        // Act: first round-trip
        var json1 = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized1 = JsonSerializer.Deserialize<IndexingState>(json1, JsonOptions);

        // Act: second round-trip
        var json2 = JsonSerializer.Serialize(deserialized1, JsonOptions);

        // Assert: both JSON strings should be identical
        json2.Should().Be(json1, "double round-trip should produce stable JSON");
    }

    [Property(MaxTest = 100)]
    public void EmptyState_RoundTrips_Correctly(PositiveInt versionSeed)
    {
        // Arrange: state with no files
        var original = new IndexingState
        {
            Version = (versionSeed.Get % 5) + 1,
            LastRunTimestamp = DateTimeOffset.Parse("2024-06-15T12:00:00Z"),
            Files = new Dictionary<string, FileState>()
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IndexingState>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Version.Should().Be(original.Version);
        deserialized.LastRunTimestamp.Should().Be(original.LastRunTimestamp);
        deserialized.Files.Should().BeEmpty();
    }
}
