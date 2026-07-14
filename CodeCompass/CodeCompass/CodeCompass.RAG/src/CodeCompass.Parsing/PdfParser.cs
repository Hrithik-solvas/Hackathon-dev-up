using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses PDF files, extracting text content from all pages in page order using PdfPig.
/// Returns empty text and empty headings for image-only PDFs.
/// </summary>
public class PdfParser : IDocumentParser
{
    private readonly ILogger<PdfParser> _logger;
    private readonly FileValidator _fileValidator;

    public PdfParser(ILogger<PdfParser> logger, FileValidator fileValidator)
    {
        _logger = logger;
        _fileValidator = fileValidator;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _fileValidator.Validate(filePath);

        _logger.LogDebug("Parsing PDF file: {FilePath}", filePath);

        var text = await Task.Run(() => ExtractText(filePath), cancellationToken);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        // PDF heading extraction is not required for this task
        var headings = Array.Empty<Heading>();

        _logger.LogDebug("Parsed PDF file {FilePath}: {TextLength} characters extracted", filePath, text.Length);

        return new ParsedDocument(text, headings, sourceMetadata);
    }

    private string ExtractText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var pageTexts = new List<string>();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                pageTexts.Add(pageText);
            }
        }

        // Return empty string for image-only PDFs (no extractable text)
        if (pageTexts.Count == 0)
        {
            _logger.LogInformation("PDF file {FilePath} contains no extractable text (image-only)", filePath);
            return string.Empty;
        }

        return string.Join(Environment.NewLine, pageTexts);
    }
}
