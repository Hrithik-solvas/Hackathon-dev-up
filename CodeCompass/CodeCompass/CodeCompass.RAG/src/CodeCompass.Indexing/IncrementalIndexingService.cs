using System.Text.Json;
using System.Text.Json.Serialization;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Indexing;

/// <summary>
/// Represents the persisted indexing state, tracking which files have been indexed
/// and their metadata at the time of indexing.
/// </summary>
public class IndexingState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("lastRunTimestamp")]
    public DateTimeOffset LastRunTimestamp { get; set; }

    [JsonPropertyName("files")]
    public Dictionary<string, FileState> Files { get; set; } = new();
}

/// <summary>
/// Represents the stored state of a single file in the indexing state.
/// </summary>
public class FileState
{
    [JsonPropertyName("lastModified")]
    public DateTimeOffset LastModified { get; set; }

    [JsonPropertyName("chunkCount")]
    public int ChunkCount { get; set; }
}

/// <summary>
/// Manages change detection and state tracking for incremental indexing.
/// Persists state as a JSON file and compares current file timestamps against
/// stored state to classify files as new, modified, deleted, or unchanged.
/// </summary>
public class IncrementalIndexingService : IIncrementalIndexingService
{
    private readonly ILogger<IncrementalIndexingService> _logger;
    private string _stateFilePath = ".codecompass-state.json";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".jsx", ".tsx", ".sql", ".js", ".ts", ".asp",
        ".md", ".pdf", ".docx"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IncrementalIndexingService(ILogger<IncrementalIndexingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sets the state file path used for persistence.
    /// </summary>
    public void SetStateFilePath(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    /// <inheritdoc />
    public async Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be null or empty.", nameof(targetPath));

        _logger.LogDebug("Computing indexing plan for directory: {TargetPath}", targetPath);

        // Load existing state
        var state = await LoadStateAsync(_stateFilePath);

        // Get current files on disk
        var currentFiles = EnumerateSupportedFiles(targetPath);

        var newFiles = new List<string>();
        var modifiedFiles = new List<string>();
        var unchangedFiles = new List<string>();
        var deletedFiles = new List<string>();

        // Classify each file on disk
        foreach (var filePath in currentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(filePath);

            if (!state.Files.TryGetValue(normalizedPath, out var storedFile))
            {
                // File is on disk but not in state → new
                newFiles.Add(filePath);
            }
            else
            {
                // File is on disk and in state → check timestamp
                var currentLastModified = GetFileLastModified(filePath);
                if (currentLastModified != storedFile.LastModified)
                {
                    modifiedFiles.Add(filePath);
                }
                else
                {
                    unchangedFiles.Add(filePath);
                }
            }
        }

        // Find deleted files: in state but not on disk
        var currentFileSet = new HashSet<string>(
            currentFiles.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);

        foreach (var storedPath in state.Files.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!currentFileSet.Contains(storedPath))
            {
                deletedFiles.Add(storedPath);
            }
        }

        _logger.LogInformation(
            "Indexing plan computed: {NewCount} new, {ModifiedCount} modified, {DeletedCount} deleted, {UnchangedCount} unchanged",
            newFiles.Count, modifiedFiles.Count, deletedFiles.Count, unchangedFiles.Count);

        return new IndexingPlan(
            NewFiles: newFiles.AsReadOnly(),
            ModifiedFiles: modifiedFiles.AsReadOnly(),
            DeletedFiles: deletedFiles.AsReadOnly(),
            UnchangedFiles: unchangedFiles.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task UpdateStateAsync(string filePath, DateTimeOffset lastModified, int chunkCount = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var normalizedPath = NormalizePath(filePath);
        _logger.LogDebug("Updating state for {FilePath} with lastModified={LastModified}, chunkCount={ChunkCount}",
            normalizedPath, lastModified, chunkCount);

        var state = await LoadStateAsync(_stateFilePath);

        state.Files[normalizedPath] = new FileState
        {
            LastModified = lastModified,
            ChunkCount = chunkCount
        };
        state.LastRunTimestamp = DateTimeOffset.UtcNow;

        await SaveStateAsync(_stateFilePath, state);
    }

    /// <inheritdoc />
    public async Task RemoveStateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var normalizedPath = NormalizePath(filePath);
        _logger.LogDebug("Removing state for {FilePath}", normalizedPath);

        var state = await LoadStateAsync(_stateFilePath);

        state.Files.Remove(normalizedPath);
        state.LastRunTimestamp = DateTimeOffset.UtcNow;

        await SaveStateAsync(_stateFilePath, state);
    }

    /// <summary>
    /// Loads the indexing state from a JSON file.
    /// Returns a new empty state if the file does not exist or is corrupted.
    /// </summary>
    public async Task<IndexingState> LoadStateAsync(string stateFilePath)
    {
        if (!File.Exists(stateFilePath))
        {
            _logger.LogDebug("State file not found at {StateFilePath}, starting with empty state", stateFilePath);
            return new IndexingState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(stateFilePath);
            var state = JsonSerializer.Deserialize<IndexingState>(json, JsonOptions);
            if (state == null)
            {
                _logger.LogWarning("State file at {StateFilePath} deserialized to null, using empty state", stateFilePath);
                return new IndexingState();
            }

            _logger.LogDebug("Loaded indexing state with {FileCount} tracked files from {StateFilePath}",
                state.Files.Count, stateFilePath);
            return state;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "State file at {StateFilePath} is corrupted, starting with empty state", stateFilePath);
            return new IndexingState();
        }
    }

    /// <summary>
    /// Saves the indexing state to a JSON file.
    /// </summary>
    public async Task SaveStateAsync(string stateFilePath, IndexingState state)
    {
        var directory = Path.GetDirectoryName(stateFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(stateFilePath, json);

        _logger.LogDebug("Saved indexing state with {FileCount} tracked files to {StateFilePath}",
            state.Files.Count, stateFilePath);
    }

    /// <summary>
    /// Enumerates all supported files in the given directory recursively.
    /// </summary>
    private List<string> EnumerateSupportedFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return new List<string>();
        }

        var files = new List<string>();
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(filePath);
                if (SupportedExtensions.Contains(extension))
                {
                    files.Add(filePath);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied when enumerating files in {DirectoryPath}", directoryPath);
        }

        return files;
    }

    /// <summary>
    /// Gets the last modified timestamp for a file.
    /// </summary>
    private static DateTimeOffset GetFileLastModified(string filePath)
    {
        return new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
    }

    /// <summary>
    /// Normalizes a file path to use forward slashes for consistent state storage.
    /// </summary>
    public static string NormalizePath(string filePath)
    {
        return filePath.Replace('\\', '/');
    }
}
