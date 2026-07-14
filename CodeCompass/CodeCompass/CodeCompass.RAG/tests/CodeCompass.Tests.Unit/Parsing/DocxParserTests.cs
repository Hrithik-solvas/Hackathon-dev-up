using CodeCompass.Core.Configuration;
using CodeCompass.Core.Models;
using CodeCompass.Parsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Unit.Parsing;

public class DocxParserTests : IDisposable
{
    private readonly DocxParser _parser;
    private readonly string _tempDir;

    public DocxParserTests()
    {
        var logger = NullLogger<DocxParser>.Instance;
        var settings = Options.Create(new IngestionSettings());
        var fileValidator = new FileValidator(settings);
        _parser = new DocxParser(logger, fileValidator);
        _tempDir = Path.Combine(Path.GetTempPath(), $"DocxParserTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region Helper Methods

    private string CreateDocxFile(Action<Body> configureBody, string fileName = "test.docx")
    {
        var filePath = Path.Combine(_tempDir, fileName);

        using (var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            configureBody(body);

            mainPart.Document.Save();
        }

        return filePath;
    }

    private static Paragraph CreateParagraph(string text, string? styleId = null)
    {
        var paragraph = new Paragraph();

        if (styleId is not null)
        {
            paragraph.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId { Val = styleId });
        }

        paragraph.AppendChild(new Run(new Text(text)));

        return paragraph;
    }

    #endregion

    #region CanParse Tests

    [Theory]
    [InlineData(".docx")]
    [InlineData(".DOCX")]
    [InlineData(".Docx")]
    [InlineData(".dOcX")]
    public void CanParse_ReturnsTrue_ForDocxExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".doc")]
    [InlineData(".pdf")]
    [InlineData(".md")]
    [InlineData(".txt")]
    [InlineData(".html")]
    [InlineData(".cs")]
    [InlineData("")]
    public void CanParse_ReturnsFalse_ForNonDocxExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeFalse();
    }

    #endregion

    #region ParseAsync - Text Extraction Tests

    [Fact]
    public async Task ParseAsync_ExtractsTextContent_PreservingParagraphBoundaries()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("First paragraph"));
            body.AppendChild(CreateParagraph("Second paragraph"));
            body.AppendChild(CreateParagraph("Third paragraph"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().Be("First paragraph\nSecond paragraph\nThird paragraph");
    }

    [Fact]
    public async Task ParseAsync_ExtractsTextFromMultipleRuns()
    {
        var filePath = CreateDocxFile(body =>
        {
            var paragraph = new Paragraph();
            paragraph.AppendChild(new Run(new Text("Hello ")));
            paragraph.AppendChild(new Run(new Text("World")));
            body.AppendChild(paragraph);
        });

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().Be("Hello World");
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyText_ForEmptyDocument()
    {
        var filePath = CreateDocxFile(body => { });

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().BeEmpty();
    }

    #endregion

    #region ParseAsync - Heading Extraction Tests

    [Fact]
    public async Task ParseAsync_ExtractsHeadings_WithCorrectLevels()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("Title", "Heading1"));
            body.AppendChild(CreateParagraph("Some text"));
            body.AppendChild(CreateParagraph("Subtitle", "Heading2"));
            body.AppendChild(CreateParagraph("More text"));
            body.AppendChild(CreateParagraph("Sub-subtitle", "Heading3"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(3);
        result.Headings[0].Should().Be(new Heading(1, "Title"));
        result.Headings[1].Should().Be(new Heading(2, "Subtitle"));
        result.Headings[2].Should().Be(new Heading(3, "Sub-subtitle"));
    }

    [Fact]
    public async Task ParseAsync_ExtractsAllHeadingLevels()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("H1", "Heading1"));
            body.AppendChild(CreateParagraph("H2", "Heading2"));
            body.AppendChild(CreateParagraph("H3", "Heading3"));
            body.AppendChild(CreateParagraph("H4", "Heading4"));
            body.AppendChild(CreateParagraph("H5", "Heading5"));
            body.AppendChild(CreateParagraph("H6", "Heading6"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(6);
        result.Headings[0].Should().Be(new Heading(1, "H1"));
        result.Headings[1].Should().Be(new Heading(2, "H2"));
        result.Headings[2].Should().Be(new Heading(3, "H3"));
        result.Headings[3].Should().Be(new Heading(4, "H4"));
        result.Headings[4].Should().Be(new Heading(5, "H5"));
        result.Headings[5].Should().Be(new Heading(6, "H6"));
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyHeadings_WhenNoHeadingsPresent()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("Just plain text"));
            body.AppendChild(CreateParagraph("Another paragraph"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_IgnoresEmptyHeadingParagraphs()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("Valid Heading", "Heading1"));
            body.AppendChild(CreateParagraph("", "Heading2"));
            body.AppendChild(CreateParagraph("   ", "Heading3"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().HaveCount(1);
        result.Headings[0].Should().Be(new Heading(1, "Valid Heading"));
    }

    #endregion

    #region ParseAsync - Source Metadata Tests

    [Fact]
    public async Task ParseAsync_PopulatesSourceMetadata_Correctly()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("Content"));
        }, "document.docx");

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.FilePath.Should().Be(new FileInfo(filePath).FullName);
        result.SourceMetadata.FileName.Should().Be("document.docx");
        result.SourceMetadata.FileExtension.Should().Be(".docx");
        result.SourceMetadata.LastModified.Should().BeCloseTo(
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ParseAsync_SourceMetadata_HasValidLastModified()
    {
        var filePath = CreateDocxFile(body =>
        {
            body.AppendChild(CreateParagraph("Test"));
        });

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.LastModified.Should().BeAfter(DateTimeOffset.MinValue);
        result.SourceMetadata.LastModified.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    #endregion
}
