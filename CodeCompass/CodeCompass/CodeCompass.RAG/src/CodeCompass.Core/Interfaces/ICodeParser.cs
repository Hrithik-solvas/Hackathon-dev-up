using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Handles parsing of code repository files (C#, JSX/TSX, SQL).
/// </summary>
public interface ICodeParser
{
    bool CanParse(string fileExtension);
    Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
