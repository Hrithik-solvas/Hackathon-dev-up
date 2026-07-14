using CodeCompass.Core.Configuration;
using CodeCompass.Core.Exceptions;
using CodeCompass.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Unit.Parsing;

public class FileValidatorTests : IDisposable
{
    private readonly FileValidator _validator;
    private readonly string _tempDir;

    public FileValidatorTests()
    {
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 50));
        _validator = new FileValidator(settings);
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileValidatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string CreateTempFile(string content, string fileName)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #region Supported Extension Tests

    [Theory]
    [InlineData("test.md")]
    [InlineData("test.pdf")]
    [InlineData("test.docx")]
    [InlineData("test.MD")]
    [InlineData("test.Pdf")]
    [InlineData("test.DOCX")]
    public void Validate_DoesNotThrow_ForSupportedExtensions(string fileName)
    {
        var filePath = CreateTempFile("content", fileName);

        var act = () => _validator.Validate(filePath);

        act.Should().NotThrow();
    }

    #endregion

    #region Unsupported Extension Tests

    [Theory]
    [InlineData("test.txt", ".txt")]
    [InlineData("test.html", ".html")]
    [InlineData("test.csv", ".csv")]
    [InlineData("test.cs", ".cs")]
    [InlineData("test.json", ".json")]
    public void Validate_ThrowsUnsupportedExtension_ForUnsupportedFileTypes(string fileName, string expectedExtension)
    {
        var filePath = CreateTempFile("content", fileName);

        var act = () => _validator.Validate(filePath);

        var exception = act.Should().Throw<FileValidationException>().Which;
        exception.ErrorKind.Should().Be(FileValidationErrorKind.UnsupportedExtension);
        exception.FilePath.Should().Be(filePath);
        exception.Message.Should().Contain(expectedExtension);
    }

    #endregion

    #region File Not Found Tests

    [Fact]
    public void Validate_ThrowsFileNotFound_WhenFileDoesNotExist()
    {
        var filePath = Path.Combine(_tempDir, "nonexistent.md");

        var act = () => _validator.Validate(filePath);

        var exception = act.Should().Throw<FileValidationException>().Which;
        exception.ErrorKind.Should().Be(FileValidationErrorKind.FileNotFound);
        exception.FilePath.Should().Be(filePath);
        exception.Message.Should().Contain(filePath);
    }

    #endregion

    #region File Too Large Tests

    [Fact]
    public void Validate_ThrowsFileTooLarge_WhenFileExceedsMaxSize()
    {
        // Use a small max size (1 MB) to make the test feasible
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 1));
        var validator = new FileValidator(settings);

        var filePath = Path.Combine(_tempDir, "large.md");
        // Create a file slightly over 1 MB
        var content = new byte[1 * 1024 * 1024 + 1];
        File.WriteAllBytes(filePath, content);

        var act = () => validator.Validate(filePath);

        var exception = act.Should().Throw<FileValidationException>().Which;
        exception.ErrorKind.Should().Be(FileValidationErrorKind.FileTooLarge);
        exception.FilePath.Should().Be(filePath);
        exception.Message.Should().Contain("maximum supported size");
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenFileIsExactlyAtMaxSize()
    {
        // Use a small max size (1 MB) to make the test feasible
        var settings = Options.Create(new IngestionSettings(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 1));
        var validator = new FileValidator(settings);

        var filePath = Path.Combine(_tempDir, "exact.md");
        // Create a file exactly at 1 MB
        var content = new byte[1 * 1024 * 1024];
        File.WriteAllBytes(filePath, content);

        var act = () => validator.Validate(filePath);

        act.Should().NotThrow();
    }

    #endregion

    #region Error Message Detail Tests

    [Fact]
    public void Validate_ErrorMessage_ContainsExtensionName_ForUnsupportedExtension()
    {
        var filePath = CreateTempFile("content", "test.xyz");

        var act = () => _validator.Validate(filePath);

        var exception = act.Should().Throw<FileValidationException>().Which;
        exception.Message.Should().Contain(".xyz");
    }

    [Fact]
    public void Validate_ErrorMessage_ContainsFilePath_ForFileNotFound()
    {
        var filePath = Path.Combine(_tempDir, "missing_file.md");

        var act = () => _validator.Validate(filePath);

        var exception = act.Should().Throw<FileValidationException>().Which;
        exception.Message.Should().Contain(filePath);
    }

    #endregion
}
