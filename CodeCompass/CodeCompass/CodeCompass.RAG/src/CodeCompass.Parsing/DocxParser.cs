using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses DOCX files, extracting text content, headings with levels, and preserving paragraph boundaries
/// using DocumentFormat.OpenXml.
/// </summary>
public class DocxParser : IDocumentParser
{
    private readonly ILogger<DocxParser> _logger;
    private readonly FileValidator _fileValidator;

    public DocxParser(ILogger<DocxParser> logger, FileValidator fileValidator)
    {
        _logger = logger;
        _fileValidator = fileValidator;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return string.Equals(fileExtension, ".docx", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _fileValidator.Validate(filePath);

        _logger.LogDebug("Parsing DOCX file: {FilePath}", filePath);

        var (text, headings) = await Task.Run(() => ExtractContent(filePath), cancellationToken);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        _logger.LogDebug("Parsed DOCX file {FilePath}: {TextLength} characters, {HeadingCount} headings extracted",
            filePath, text.Length, headings.Count);

        return new ParsedDocument(text, headings, sourceMetadata);
    }

    private (string Text, List<Heading> Headings) ExtractContent(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            _logger.LogInformation("DOCX file {FilePath} has no document body", filePath);
            return (string.Empty, new List<Heading>());
        }

        var paragraphTexts = new List<string>();
        var headings = new List<Heading>();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var paragraphText = GetParagraphText(paragraph);
            paragraphTexts.Add(paragraphText);

            var headingLevel = GetHeadingLevel(paragraph);
            if (headingLevel.HasValue && !string.IsNullOrWhiteSpace(paragraphText))
            {
                headings.Add(new Heading(headingLevel.Value, paragraphText.Trim()));
            }
        }

        var text = string.Join("\n", paragraphTexts);

        return (text, headings);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var runs = paragraph.Elements<Run>();
        return string.Concat(runs.Select(run => run.InnerText));
    }

    private static int? GetHeadingLevel(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

        if (string.IsNullOrEmpty(styleId))
        {
            return null;
        }

        // Check for "Heading1" through "Heading6" style IDs (case-insensitive)
        if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) &&
            styleId.Length >= 8)
        {
            var levelPart = styleId.Substring(7);
            if (int.TryParse(levelPart, out var level) && level >= 1 && level <= 6)
            {
                return level;
            }
        }

        // Check outline level in paragraph properties
        var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
        if (outlineLevel.HasValue && outlineLevel.Value >= 0 && outlineLevel.Value <= 5)
        {
            // OutlineLevel is zero-based (0 = level 1, 5 = level 6)
            return outlineLevel.Value + 1;
        }

        return null;
    }
}
