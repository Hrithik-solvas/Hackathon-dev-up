using CodeCompass.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Parsing;

public class RepositoryEnumeratorTests : IDisposable
{
    private readonly RepositoryEnumerator _enumerator;
    private readonly string _tempDir;

    public RepositoryEnumeratorTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<RepositoryEnumerator>();
        _enumerator = new RepositoryEnumerator(logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"RepositoryEnumeratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string CreateFile(string relativePath, string content = "")
    {
        var filePath = Path.Combine(_tempDir, relativePath);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #region Validation Tests

    [Fact]
    public void EnumerateFiles_ThrowsArgumentException_WhenPathIsNull()
    {
        var act = () => _enumerator.EnumerateFiles(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnumerateFiles_ThrowsArgumentException_WhenPathIsEmpty()
    {
        var act = () => _enumerator.EnumerateFiles("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnumerateFiles_ThrowsArgumentException_WhenPathIsWhitespace()
    {
        var act = () => _enumerator.EnumerateFiles("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnumerateFiles_ThrowsDirectoryNotFoundException_WhenPathDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent");
        var act = () => _enumerator.EnumerateFiles(nonExistentPath);
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    #endregion

    #region Extension Filtering Tests

    [Fact]
    public void EnumerateFiles_ReturnsCSharpFiles()
    {
        var csFile = CreateFile("Program.cs", "class Program {}");
        CreateFile("readme.txt", "ignore");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().ContainSingle()
            .Which.Should().Be(csFile);
    }

    [Fact]
    public void EnumerateFiles_ReturnsJsxFiles()
    {
        var jsxFile = CreateFile("App.jsx", "export default function App() {}");
        CreateFile("style.css", "body {}");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().ContainSingle()
            .Which.Should().Be(jsxFile);
    }

    [Fact]
    public void EnumerateFiles_ReturnsTsxFiles()
    {
        var tsxFile = CreateFile("Component.tsx", "export const Comp: React.FC = () => <div/>;");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().ContainSingle()
            .Which.Should().Be(tsxFile);
    }

    [Fact]
    public void EnumerateFiles_ReturnsSqlFiles()
    {
        var sqlFile = CreateFile("schema.sql", "CREATE TABLE Users (Id INT);");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().ContainSingle()
            .Which.Should().Be(sqlFile);
    }

    [Fact]
    public void EnumerateFiles_ExcludesUnsupportedExtensions()
    {
        CreateFile("readme.md", "# Hello");
        CreateFile("document.pdf", "pdf content");
        CreateFile("style.css", "body {}");
        CreateFile("data.json", "{}");
        CreateFile("script.js", "console.log('hi')");
        CreateFile("index.html", "<html/>");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateFiles_ReturnsAllSupportedExtensions()
    {
        var csFile = CreateFile("Program.cs");
        var jsxFile = CreateFile("App.jsx");
        var tsxFile = CreateFile("Component.tsx");
        var sqlFile = CreateFile("query.sql");
        CreateFile("readme.md");
        CreateFile("style.css");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().HaveCount(4);
        results.Should().Contain(csFile);
        results.Should().Contain(jsxFile);
        results.Should().Contain(tsxFile);
        results.Should().Contain(sqlFile);
    }

    [Theory]
    [InlineData(".CS")]
    [InlineData(".Cs")]
    [InlineData(".JSX")]
    [InlineData(".Tsx")]
    [InlineData(".SQL")]
    public void EnumerateFiles_IsCaseInsensitive_ForExtensions(string extension)
    {
        var file = CreateFile($"test{extension}");

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().ContainSingle()
            .Which.Should().Be(file);
    }

    #endregion

    #region Recursive Enumeration Tests

    [Fact]
    public void EnumerateFiles_RecursesIntoSubdirectories()
    {
        var rootFile = CreateFile("Root.cs");
        var nestedFile = CreateFile(Path.Combine("src", "Nested.cs"));
        var deepFile = CreateFile(Path.Combine("src", "models", "Deep.cs"));

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().HaveCount(3);
        results.Should().Contain(rootFile);
        results.Should().Contain(nestedFile);
        results.Should().Contain(deepFile);
    }

    [Fact]
    public void EnumerateFiles_ReturnsEmptyList_WhenNoSupportedFiles()
    {
        CreateFile("readme.md");
        CreateFile(Path.Combine("docs", "guide.pdf"));

        var results = _enumerator.EnumerateFiles(_tempDir);

        results.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateFiles_ReturnsEmptyList_ForEmptyDirectory()
    {
        var results = _enumerator.EnumerateFiles(_tempDir);
        results.Should().BeEmpty();
    }

    #endregion
}
