using CodeCompass.Core.Configuration;
using CodeCompass.Core.Models;
using CodeCompass.Parsing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 1: Parser output structure invariant.
/// For any valid input file that is successfully parsed, the output contains non-null raw text,
/// a headings list (possibly empty), and complete source file metadata with all fields populated.
/// 
/// **Validates: Requirements 1.5, 2.7**
/// </summary>
public class ParserOutputStructureProperty
{
    private static readonly IngestionSettings DefaultSettings = new(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 50);
    private static readonly IOptions<IngestionSettings> SettingsOptions = Options.Create(DefaultSettings);
    private static readonly FileValidator Validator = new(SettingsOptions);

    private static MarkdownParser CreateMarkdownParser() =>
        new(NullLogger<MarkdownParser>.Instance, Validator);

    private static PdfParser CreatePdfParser() =>
        new(NullLogger<PdfParser>.Instance, Validator);

    private static DocxParser CreateDocxParser() =>
        new(NullLogger<DocxParser>.Instance, Validator);

    [Property(MaxTest = 100)]
    public void MarkdownParser_OutputHasNonNullRawText_HeadingsList_AndCompleteMetadata(PositiveInt headingSeed, PositiveInt bodySeed)
    {
        var headingCount = headingSeed.Get % 6; // 0-5 headings
        var bodyLineCount = (bodySeed.Get % 5) + 1; // 1-5 body lines

        var content = GenerateMarkdownContent(headingCount, bodyLineCount);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");

        try
        {
            File.WriteAllText(tempFile, content);
            var parser = CreateMarkdownParser();
            var result = parser.ParseAsync(tempFile).GetAwaiter().GetResult();

            AssertParsedDocumentStructure(result, tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Property(MaxTest = 100)]
    public void DocxParser_OutputHasNonNullRawText_HeadingsList_AndCompleteMetadata(PositiveInt paragraphSeed)
    {
        var paragraphCount = (paragraphSeed.Get % 5) + 1; // 1-5 paragraphs

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");

        try
        {
            CreateMinimalDocx(tempFile, paragraphCount);
            var parser = CreateDocxParser();
            var result = parser.ParseAsync(tempFile).GetAwaiter().GetResult();

            AssertParsedDocumentStructure(result, tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Property(MaxTest = 100)]
    public void PdfParser_OutputHasNonNullRawText_HeadingsList_AndCompleteMetadata(PositiveInt seed)
    {
        var textVariant = seed.Get % 10;

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");

        try
        {
            CreateMinimalPdf(tempFile, textVariant);
            var parser = CreatePdfParser();
            var result = parser.ParseAsync(tempFile).GetAwaiter().GetResult();

            AssertParsedDocumentStructure(result, tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Asserts the structural invariants of a ParsedDocument (Property 1):
    /// - Non-null raw text
    /// - A headings list (possibly empty but not null)
    /// - Complete source file metadata with all fields populated (non-null/non-default)
    /// </summary>
    private static void AssertParsedDocumentStructure(ParsedDocument result, string filePath)
    {
        // Non-null raw text
        result.RawText.Should().NotBeNull("raw text must never be null for a successfully parsed document");

        // Headings list is not null (possibly empty)
        result.Headings.Should().NotBeNull("headings list must never be null, even if empty");

        // Complete source file metadata with all fields populated
        result.SourceMetadata.Should().NotBeNull("source metadata must be present");
        result.SourceMetadata.FilePath.Should().NotBeNullOrWhiteSpace("FilePath must be populated");
        result.SourceMetadata.FileName.Should().NotBeNullOrWhiteSpace("FileName must be populated");
        result.SourceMetadata.FileExtension.Should().NotBeNullOrWhiteSpace("FileExtension must be populated");
        result.SourceMetadata.LastModified.Should().NotBe(default(DateTimeOffset), "LastModified must not be default");

        // Metadata fields should correspond to the actual file
        var fileInfo = new FileInfo(filePath);
        result.SourceMetadata.FilePath.Should().Be(fileInfo.FullName);
        result.SourceMetadata.FileName.Should().Be(fileInfo.Name);
        result.SourceMetadata.FileExtension.Should().Be(fileInfo.Extension);
    }

    /// <summary>
    /// Generates markdown content with the specified number of headings and body lines.
    /// </summary>
    private static string GenerateMarkdownContent(int headingCount, int bodyLineCount)
    {
        var lines = new List<string>();
        var bodyTexts = new[] { "Some paragraph text.", "Another line of content.", "Details about the implementation.", "A short note.", "Final remark." };
        var headingTexts = new[] { "Introduction", "Overview", "Details", "Summary", "Conclusion", "Appendix" };

        for (var i = 0; i < headingCount; i++)
        {
            var level = (i % 6) + 1;
            var text = headingTexts[i % headingTexts.Length];
            lines.Add($"{new string('#', level)} {text}");
            lines.Add(string.Empty);
        }

        for (var i = 0; i < bodyLineCount; i++)
        {
            lines.Add(bodyTexts[i % bodyTexts.Length]);
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Creates a minimal DOCX file with the specified number of paragraphs.
    /// </summary>
    private static void CreateMinimalDocx(string filePath, int paragraphCount)
    {
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        var body = new DocumentFormat.OpenXml.Wordprocessing.Body();

        for (var i = 0; i < paragraphCount; i++)
        {
            var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                new DocumentFormat.OpenXml.Wordprocessing.Run(
                    new DocumentFormat.OpenXml.Wordprocessing.Text($"Paragraph {i + 1} content.")));
            body.Append(paragraph);
        }

        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
        mainPart.Document.Save();
    }

    /// <summary>
    /// Creates a minimal PDF file with text content using raw PDF syntax.
    /// </summary>
    private static void CreateMinimalPdf(string filePath, int textVariant)
    {
        var texts = new[]
        {
            "Hello World", "Test Document", "Sample Text", "Property Test",
            "Content Here", "PDF Example", "Generated File", "Arbitrary Input",
            "Document Body", "Page Content"
        };
        var text = texts[textVariant % texts.Length];

        // Build a minimal valid PDF with the chosen text
        var streamContent = $"BT /F1 12 Tf 100 700 Td ({text}) Tj ET";
        var streamLength = streamContent.Length;

        var pdfContent = $@"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj

2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj

3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj

4 0 obj
<< /Length {streamLength} >>
stream
{streamContent}
endstream
endobj

5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj

xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000266 00000 n 
0000000360 00000 n 

trailer
<< /Size 6 /Root 1 0 R >>
startxref
441
%%EOF";

        File.WriteAllText(filePath, pdfContent);
    }
}
