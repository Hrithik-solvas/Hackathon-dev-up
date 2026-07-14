using CodeCompass.Core.Configuration;
using CodeCompass.Core.Exceptions;
using CodeCompass.Parsing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 2: File validation and dispatch.
/// For any file path, if extension is supported and size ≤ 50 MB the parser accepts it;
/// unsupported extension returns extension error; oversized file returns size error.
///
/// **Validates: Requirements 1.4, 1.7**
/// </summary>
public class FileValidationAndDispatchProperty
{
    private static readonly IngestionSettings DefaultSettings = new(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 50);
    private static readonly IOptions<IngestionSettings> SettingsOptions = Options.Create(DefaultSettings);
    private static readonly FileValidator Validator = new(SettingsOptions);

    private static readonly string[] SupportedExtensions = { ".md", ".pdf", ".docx" };
    private static readonly string[] UnsupportedExtensions = { ".txt", ".csv", ".html", ".xml", ".json", ".log", ".exe", ".dll", ".jpg", ".png" };

    /// <summary>
    /// For any file with a supported extension and size ≤ 50 MB, the validator accepts it (no exception).
    /// </summary>
    [Property(MaxTest = 100)]
    public void SupportedExtension_WithinSizeLimit_IsAccepted(PositiveInt sizeSeed, PositiveInt extSeed)
    {
        var extension = SupportedExtensions[extSeed.Get % SupportedExtensions.Length];
        // Generate file size between 1 byte and 50 MB
        var maxSizeBytes = 50L * 1024 * 1024;
        var fileSize = (sizeSeed.Get % maxSizeBytes) + 1;

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

        try
        {
            CreateFileWithSize(tempFile, fileSize, extension);

            var act = () => Validator.Validate(tempFile);

            act.Should().NotThrow<FileValidationException>(
                $"files with supported extension '{extension}' and size {fileSize} bytes (≤ 50 MB) should be accepted");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// For any file with an unsupported extension, the validator throws a FileValidationException
    /// with ErrorKind = UnsupportedExtension.
    /// </summary>
    [Property(MaxTest = 100)]
    public void UnsupportedExtension_ReturnsExtensionError(PositiveInt extSeed, PositiveInt sizeSeed)
    {
        var extension = UnsupportedExtensions[extSeed.Get % UnsupportedExtensions.Length];
        // Use a small file size to isolate the extension check
        var fileSize = (sizeSeed.Get % 1024) + 1;

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

        try
        {
            using (var fs = File.Create(tempFile))
            {
                fs.SetLength(fileSize);
            }

            var act = () => Validator.Validate(tempFile);

            act.Should().Throw<FileValidationException>()
                .Which.ErrorKind.Should().Be(FileValidationErrorKind.UnsupportedExtension);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// For any file exceeding 50 MB (even with a supported extension), the validator throws
    /// a FileValidationException with ErrorKind = FileTooLarge.
    /// </summary>
    [Property(MaxTest = 100)]
    public void OversizedFile_ReturnsSizeError(PositiveInt overSeed, PositiveInt extSeed)
    {
        var extension = SupportedExtensions[extSeed.Get % SupportedExtensions.Length];
        // Generate a file size that exceeds 50 MB (50 MB + 1 byte to 50 MB + ~10 MB)
        var maxSizeBytes = 50L * 1024 * 1024;
        var excessBytes = (overSeed.Get % (10L * 1024 * 1024)) + 1;
        var fileSize = maxSizeBytes + excessBytes;

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

        try
        {
            // Create a sparse file with the oversized length
            using (var fs = File.Create(tempFile))
            {
                fs.SetLength(fileSize);
            }

            var act = () => Validator.Validate(tempFile);

            act.Should().Throw<FileValidationException>()
                .Which.ErrorKind.Should().Be(FileValidationErrorKind.FileTooLarge);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Creates a file with the specified size. For supported extensions that require valid format,
    /// creates a minimal valid file. Otherwise, creates a sparse file with the given size.
    /// </summary>
    private static void CreateFileWithSize(string filePath, long fileSize, string extension)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".md":
                // Markdown files are plain text, any content works
                using (var fs = File.Create(filePath))
                {
                    fs.SetLength(fileSize);
                }
                break;

            case ".pdf":
                // Create a minimal valid PDF, then set length
                CreateMinimalPdf(filePath);
                // If requested size is larger than the minimal PDF, extend it
                var pdfInfo = new FileInfo(filePath);
                if (fileSize > pdfInfo.Length)
                {
                    using var fs = new FileStream(filePath, FileMode.Open);
                    fs.SetLength(fileSize);
                }
                break;

            case ".docx":
                // Create a minimal valid DOCX
                CreateMinimalDocx(filePath);
                // If requested size is larger, extend it
                var docxInfo = new FileInfo(filePath);
                if (fileSize > docxInfo.Length)
                {
                    using var fs = new FileStream(filePath, FileMode.Open);
                    fs.SetLength(fileSize);
                }
                break;

            default:
                using (var fs = File.Create(filePath))
                {
                    fs.SetLength(fileSize);
                }
                break;
        }
    }

    private static void CreateMinimalPdf(string filePath)
    {
        var streamContent = "BT /F1 12 Tf 100 700 Td (Test) Tj ET";
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

    private static void CreateMinimalDocx(string filePath)
    {
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        var body = new DocumentFormat.OpenXml.Wordprocessing.Body(
            new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                new DocumentFormat.OpenXml.Wordprocessing.Run(
                    new DocumentFormat.OpenXml.Wordprocessing.Text("Test content."))));

        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
        mainPart.Document.Save();
    }
}
