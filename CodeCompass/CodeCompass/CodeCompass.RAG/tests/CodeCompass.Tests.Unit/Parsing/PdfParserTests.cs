using CodeCompass.Core.Configuration;
using CodeCompass.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Writer;

namespace CodeCompass.Tests.Unit.Parsing;

public class PdfParserTests : IDisposable
{
    private readonly PdfParser _parser;
    private readonly string _tempDir;

    public PdfParserTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<PdfParser>();
        var settings = Options.Create(new IngestionSettings());
        var fileValidator = new FileValidator(settings);
        _parser = new PdfParser(logger, fileValidator);
        _tempDir = Path.Combine(Path.GetTempPath(), $"PdfParserTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string CreateTempPdfWithText(string text, string fileName = "test.pdf")
    {
        var filePath = Path.Combine(_tempDir, fileName);

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), font);

        var pdfBytes = builder.Build();
        File.WriteAllBytes(filePath, pdfBytes);
        return filePath;
    }

    private string CreateTempEmptyPdf(string fileName = "empty.pdf")
    {
        var filePath = Path.Combine(_tempDir, fileName);

        var builder = new PdfDocumentBuilder();
        // Add a page with no text content (simulates image-only PDF)
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);

        var pdfBytes = builder.Build();
        File.WriteAllBytes(filePath, pdfBytes);
        return filePath;
    }

    #region CanParse Tests

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".PDF")]
    [InlineData(".Pdf")]
    [InlineData(".pDf")]
    public void CanParse_ReturnsTrue_ForPdfExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".docx")]
    [InlineData(".html")]
    [InlineData(".cs")]
    [InlineData("")]
    public void CanParse_ReturnsFalse_ForNonPdfExtensions(string extension)
    {
        _parser.CanParse(extension).Should().BeFalse();
    }

    #endregion

    #region ParseAsync - Text Extraction Tests

    [Fact]
    public async Task ParseAsync_ExtractsText_FromValidPdf()
    {
        var expectedText = "Hello World from PDF";
        var filePath = CreateTempPdfWithText(expectedText);

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().Contain("Hello");
        result.RawText.Should().Contain("World");
        result.RawText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyText_ForImageOnlyPdf()
    {
        var filePath = CreateTempEmptyPdf();

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyHeadings_Always()
    {
        var filePath = CreateTempPdfWithText("Some content");

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmptyHeadings_ForImageOnlyPdf()
    {
        var filePath = CreateTempEmptyPdf();

        var result = await _parser.ParseAsync(filePath);

        result.Headings.Should().BeEmpty();
    }

    #endregion

    #region ParseAsync - Multi-page Tests

    [Fact]
    public async Task ParseAsync_ExtractsText_FromMultiplePages()
    {
        var filePath = CreateMultiPagePdf();

        var result = await _parser.ParseAsync(filePath);

        result.RawText.Should().Contain("Page1");
        result.RawText.Should().Contain("Page2");
        // Page 1 text should appear before page 2 text (page order)
        result.RawText.IndexOf("Page1", StringComparison.Ordinal)
            .Should().BeLessThan(result.RawText.IndexOf("Page2", StringComparison.Ordinal));
    }

    #endregion

    #region ParseAsync - Source Metadata Tests

    [Fact]
    public async Task ParseAsync_PopulatesSourceMetadata_Correctly()
    {
        var filePath = CreateTempPdfWithText("Test content", "document.pdf");

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.FilePath.Should().Be(new FileInfo(filePath).FullName);
        result.SourceMetadata.FileName.Should().Be("document.pdf");
        result.SourceMetadata.FileExtension.Should().Be(".pdf");
        result.SourceMetadata.LastModified.Should().BeCloseTo(
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ParseAsync_SourceMetadata_HasValidLastModified()
    {
        var filePath = CreateTempPdfWithText("Test");

        var result = await _parser.ParseAsync(filePath);

        result.SourceMetadata.LastModified.Should().BeAfter(DateTimeOffset.MinValue);
        result.SourceMetadata.LastModified.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    #endregion

    private string CreateMultiPagePdf(string fileName = "multipage.pdf")
    {
        var filePath = Path.Combine(_tempDir, fileName);

        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        var page1 = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page1.AddText("Page1 content here", 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), font);

        var page2 = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page2.AddText("Page2 content here", 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), font);

        var pdfBytes = builder.Build();
        File.WriteAllBytes(filePath, pdfBytes);
        return filePath;
    }
}
