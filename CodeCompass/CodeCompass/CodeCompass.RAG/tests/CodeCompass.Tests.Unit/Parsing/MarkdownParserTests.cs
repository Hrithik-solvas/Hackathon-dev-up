using CodeCompass.Core.Configuration;
using CodeCompass.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Unit.Parsing;

public class MarkdownParserTests : IDisposable
{
    private readonly MarkdownParser _parser;
    private readonly string _tempDir;

    public MarkdownParserTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<MarkdownParser>();
        var settings = Options.Create(new IngestionSettings());
        var fileValidator = new FileValidator(settings);
        _parser = new MarkdownParser(logger, fileValidator);
        _tempDir = Path.Combine(Path.GetTempPath(), $"MarkdownParserTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string CreateTempFile(string content, string fileName = "test.md")
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #region CanParse Tests

    [Theory]
    [InlineData(".md")]
    [InlineData(".MD")]
    [InlineData(".Md")]
    [InlineData(".mD")]
    public void CanParse_ReturnsTrue_ForMarkdownExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".html")]
    [InlineData(".cs")]
    [InlineData("")]
    public void CanParse_ReturnsFalse_ForNonMarkdownExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeFalse();
    }

    #endregion

    #region ParseAsync - Heading Extraction Tests

    [Fact]
    public async Task ParseAsync_ExtractsHeadings_AllLevels()
    {
        var content = """
            # Heading 1
            Some text
            ## Heading 2
            More text
            ### Heading 3
            #### Heading 4
            ##### Heading 5
            ###### Heading 6
            """;
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(6);
        result.Headings[0].Should().Be(new Core.Models.Heading(1, "Heading 1"));
        result.Headings[1].Should().Be(new Core.Models.Heading(2, "Heading 2"));
        result.Headings[2].Should().Be(new Core.Models.Heading(3, "Heading 3"));
        result.Headings[3].Should().Be(new Core.Models.Heading(4, "Heading 4"));
        result.Headings[4].Should().Be(new Core.Models.Heading(5, "Heading 5"));
        result.Headings[5].Should().Be(new Core.Models.Heading(6, "Heading 6"));
    }

    [Fact]
    public async Task ParseAsync_ExtractsNoHeadings_WhenNonePresent()
    {
        var content = "Just plain text without any headings.\nAnother line.";
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_IgnoresInvalidHeadings_MoreThanSixHashes()
    {
        var content = """
            # Valid Heading
            ####### Not a heading (7 hashes)
            ## Another valid heading
            """;
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(2);
        result.Headings[0].Text.Should().Be("Valid Heading");
        result.Headings[1].Text.Should().Be("Another valid heading");
    }

    [Fact]
    public async Task ParseAsync_IgnoresHashesWithoutSpace()
    {
        var content = """
            # Valid Heading
            #NoSpace
            ##AlsoNoSpace
            ## Valid Too
            """;
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(2);
        result.Headings[0].Text.Should().Be("Valid Heading");
        result.Headings[1].Text.Should().Be("Valid Too");
    }

    #endregion

    #region ParseAsync - Raw Text Tests

    [Fact]
    public async Task ParseAsync_ReturnsFullRawText()
    {
        var content = "# Title\n\nSome paragraph text.\n\n## Section\n\nMore content here.";
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().Be(content);
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyRawText_ForEmptyFile()
    {
        var filePath = CreateTempFile(string.Empty);

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().BeEmpty();
        result.Headings.Should().BeEmpty();
    }

    #endregion

    #region ParseAsync - Source Metadata Tests

    [Fact]
    public async Task ParseAsync_PopulatesSourceMetadata_Correctly()
    {
        var content = "# Test";
        var filePath = CreateTempFile(content, "document.md");

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.FilePath.Should().Be(new FileInfo(filePath).FullName);
        result.SourceMetadata.FileName.Should().Be("document.md");
        result.SourceMetadata.FileExtension.Should().Be(".md");
        result.SourceMetadata.LastModified.Should().BeCloseTo(
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ParseAsync_SourceMetadata_HasValidLastModified()
    {
        var content = "# Test";
        var filePath = CreateTempFile(content);

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.LastModified.Should().BeAfter(DateTimeOffset.MinValue);
        result.SourceMetadata.LastModified.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    #endregion
}
