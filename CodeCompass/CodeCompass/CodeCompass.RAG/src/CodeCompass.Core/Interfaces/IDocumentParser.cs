using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Handles parsing of document files (Markdown, PDF, DOCX).
/// </summary>
public interface IDocumentParser
{
    bool CanParse(string fileExtension);
    Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
