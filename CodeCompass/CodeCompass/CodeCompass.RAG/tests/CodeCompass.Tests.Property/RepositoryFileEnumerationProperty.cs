using CodeCompass.Parsing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 4: Repository file enumeration by extension.
/// For any directory tree provided as a repository path, the file enumeration returns
/// exactly those files with extensions .cs, .jsx, .tsx, or .sql, and no files with other extensions.
/// 
/// **Validates: Requirements 2.1**
/// </summary>
public class RepositoryFileEnumerationProperty
{
    private static readonly string[] SupportedExtensions = { ".cs", ".jsx", ".tsx", ".sql" };

    private static readonly string[] UnsupportedExtensions =
    {
        ".txt", ".md", ".pdf", ".docx", ".json", ".xml", ".html", ".css",
        ".png", ".jpg", ".exe", ".dll", ".yaml", ".log", ".py", ".rb"
    };

    private static RepositoryEnumerator CreateEnumerator() =>
        new(NullLogger<RepositoryEnumerator>.Instance);

    [Property(MaxTest = 100)]
    public void EnumeratedFiles_ContainOnlySupportedExtensions(PositiveInt supportedSeed, PositiveInt unsupportedSeed)
    {
        var supportedCount = (supportedSeed.Get % 10) + 1; // 1-10 supported files
        var unsupportedCount = (unsupportedSeed.Get % 10) + 1; // 1-10 unsupported files

        var tempDir = Path.Combine(Path.GetTempPath(), $"repo_enum_test_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var expectedFiles = CreateSupportedFiles(tempDir, supportedCount);
            CreateUnsupportedFiles(tempDir, unsupportedCount);

            var enumerator = CreateEnumerator();
            var result = enumerator.EnumerateFiles(tempDir);

            // Every returned file must have a supported extension
            result.Should().AllSatisfy(filePath =>
                SupportedExtensions.Should().Contain(
                    Path.GetExtension(filePath).ToLowerInvariant(),
                    $"File '{filePath}' has an unsupported extension"));
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Property(MaxTest = 100)]
    public void EnumeratedFiles_IncludeAllSupportedFiles(PositiveInt supportedSeed, PositiveInt unsupportedSeed)
    {
        var supportedCount = (supportedSeed.Get % 10) + 1; // 1-10 supported files
        var unsupportedCount = (unsupportedSeed.Get % 10) + 1; // 1-10 unsupported files

        var tempDir = Path.Combine(Path.GetTempPath(), $"repo_enum_test_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var expectedFiles = CreateSupportedFiles(tempDir, supportedCount);
            CreateUnsupportedFiles(tempDir, unsupportedCount);

            var enumerator = CreateEnumerator();
            var result = enumerator.EnumerateFiles(tempDir);

            // All supported files must be present in the result (completeness)
            var normalizedResult = result.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var normalizedExpected = expectedFiles.Select(NormalizePath).ToList();

            normalizedResult.Should().Contain(normalizedExpected,
                "all files with supported extensions must be included in the results");
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Property(MaxTest = 100)]
    public void EnumeratedFiles_ExactlyMatchSupportedFilesInDirectoryTree(
        PositiveInt supportedSeed, PositiveInt unsupportedSeed, PositiveInt depthSeed)
    {
        var supportedCount = (supportedSeed.Get % 8) + 1; // 1-8 supported files
        var unsupportedCount = (unsupportedSeed.Get % 8) + 1; // 1-8 unsupported files
        var subDirDepth = (depthSeed.Get % 3) + 1; // 1-3 levels of nesting

        var tempDir = Path.Combine(Path.GetTempPath(), $"repo_enum_test_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            // Create files in root and nested subdirectories
            var allExpectedFiles = new List<string>();

            // Root-level files
            allExpectedFiles.AddRange(CreateSupportedFiles(tempDir, supportedCount));
            CreateUnsupportedFiles(tempDir, unsupportedCount);

            // Nested directory files
            var currentDir = tempDir;
            for (var depth = 0; depth < subDirDepth; depth++)
            {
                currentDir = Path.Combine(currentDir, $"sub_{depth}");
                Directory.CreateDirectory(currentDir);
                allExpectedFiles.AddRange(CreateSupportedFiles(currentDir, supportedCount));
                CreateUnsupportedFiles(currentDir, unsupportedCount);
            }

            var enumerator = CreateEnumerator();
            var result = enumerator.EnumerateFiles(tempDir);

            // Result count must match expected supported file count exactly
            var normalizedResult = result.Select(NormalizePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var normalizedExpected = allExpectedFiles.Select(NormalizePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

            normalizedResult.Should().BeEquivalentTo(normalizedExpected,
                "enumeration must return exactly the set of files with supported extensions");
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    /// <summary>
    /// Creates files with supported code extensions in the specified directory.
    /// Returns the list of created file paths.
    /// </summary>
    private static List<string> CreateSupportedFiles(string directory, int count)
    {
        var files = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var extension = SupportedExtensions[i % SupportedExtensions.Length];
            var fileName = $"file_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, $"// content for {fileName}");
            files.Add(filePath);
        }
        return files;
    }

    /// <summary>
    /// Creates files with unsupported extensions in the specified directory.
    /// </summary>
    private static void CreateUnsupportedFiles(string directory, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var extension = UnsupportedExtensions[i % UnsupportedExtensions.Length];
            var fileName = $"file_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, $"content for {fileName}");
        }
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
