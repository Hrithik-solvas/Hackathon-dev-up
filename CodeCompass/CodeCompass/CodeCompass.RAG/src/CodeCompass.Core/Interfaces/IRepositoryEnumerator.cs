namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Recursively enumerates supported code files within a repository directory tree.
/// </summary>
public interface IRepositoryEnumerator
{
    /// <summary>
    /// Recursively enumerates all files with supported code extensions (.cs, .jsx, .tsx, .sql)
    /// within the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The root directory path to enumerate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of file paths with supported code extensions.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the specified directory is not accessible.</exception>
    IReadOnlyList<string> EnumerateFiles(string directoryPath);
}
