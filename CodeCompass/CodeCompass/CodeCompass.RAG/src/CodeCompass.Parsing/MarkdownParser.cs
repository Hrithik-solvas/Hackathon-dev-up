using System.Text.RegularExpressions;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses Markdown files, extracting full text content and headings (levels 1–6).
/// </summary>
public partial class MarkdownParser : IDocumentParser
{
    private readonly ILogger<MarkdownParser> _logger;
    private readonly FileValidator _fileValidator;

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public MarkdownParser(ILogger<MarkdownParser> logger, FileValidator fileValidator)
    {
        _logger = logger;
        _fileValidator = fileValidator;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return string.Equals(fileExtension, ".md", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _fileValidator.Validate(filePath);

        _logger.LogDebug("Parsing Markdown file: {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var headings = ExtractHeadings(content);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        _logger.LogDebug("Parsed Markdown file {FilePath}: {HeadingCount} headings found", filePath, headings.Count);

        return new ParsedDocument(content, headings, sourceMetadata);
    }

    private static List<Heading> ExtractHeadings(string content)
    {
        var headings = new List<Heading>();
        var matches = HeadingRegex.Matches(content);

        foreach (Match match in matches)
        {
            var level = match.Groups[1].Value.Length;
            var text = match.Groups[2].Value.Trim();
            headings.Add(new Heading(level, text));
        }

        return headings;
    }
}
