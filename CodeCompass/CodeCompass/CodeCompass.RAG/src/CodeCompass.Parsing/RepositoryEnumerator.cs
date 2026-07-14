using CodeCompass.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Recursively enumerates supported code files (.cs, .jsx, .tsx, .sql) within
/// a given directory path, skipping inaccessible subdirectories gracefully.
/// </summary>
public class RepositoryEnumerator : IRepositoryEnumerator
{
    private readonly ILogger<RepositoryEnumerator> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".jsx",
        ".tsx",
        ".sql"
    };

    public RepositoryEnumerator(ILogger<RepositoryEnumerator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateFiles(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException(
                $"The specified directory does not exist: {directoryPath}");
        }

        // Verify the directory is accessible by attempting to get its attributes
        try
        {
            Directory.GetFileSystemEntries(directoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                $"The specified directory is not accessible: {directoryPath}", ex);
        }

        _logger.LogDebug("Enumerating code files in directory: {DirectoryPath}", directoryPath);

        var files = new List<string>();
        EnumerateRecursive(directoryPath, files);

        _logger.LogDebug(
            "Enumerated {FileCount} supported code files in directory: {DirectoryPath}",
            files.Count, directoryPath);

        return files;
    }

    private void EnumerateRecursive(string currentPath, List<string> results)
    {
        // Enumerate files in the current directory
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(currentPath))
            {
                var extension = Path.GetExtension(filePath);
                if (SupportedExtensions.Contains(extension))
                {
                    results.Add(filePath);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Access denied when enumerating files in directory: {DirectoryPath}. Skipping.",
                currentPath);
            return;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "I/O error when enumerating files in directory: {DirectoryPath}. Skipping.",
                currentPath);
            return;
        }

        // Recursively enumerate subdirectories
        IEnumerable<string> subdirectories;
        try
        {
            subdirectories = Directory.EnumerateDirectories(currentPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Access denied when enumerating subdirectories of: {DirectoryPath}. Skipping.",
                currentPath);
            return;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "I/O error when enumerating subdirectories of: {DirectoryPath}. Skipping.",
                currentPath);
            return;
        }

        foreach (var subdirectory in subdirectories)
        {
            EnumerateRecursive(subdirectory, results);
        }
    }
}
