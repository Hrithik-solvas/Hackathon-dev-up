using System.Text.RegularExpressions;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses SQL source files using regex-based pattern matching, extracting
/// stored procedure names, parameters, and comment blocks.
/// </summary>
public partial class SqlCodeParser : ICodeParser
{
    private readonly ILogger<SqlCodeParser> _logger;

    public SqlCodeParser(ILogger<SqlCodeParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return string.Equals(fileExtension, ".sql", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Parsing SQL file: {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var symbols = ExtractSymbols(content);
        var documentationComments = ExtractComments(content);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        _logger.LogDebug(
            "Parsed SQL file {FilePath}: {SymbolCount} symbols, {CommentCount} documentation comments found",
            filePath, symbols.Count, documentationComments.Count);

        return new ParsedCode(content, symbols, documentationComments, sourceMetadata);
    }

    private static List<CodeSymbol> ExtractSymbols(string content)
    {
        var symbols = new List<CodeSymbol>();

        foreach (Match match in StoredProcedureRegex().Matches(content))
        {
            var procedureName = match.Groups["name"].Value;
            symbols.Add(new CodeSymbol(procedureName, CodeSymbolKind.StoredProcedure, null));

            // Extract parameters for this procedure by scanning after the procedure declaration
            var parametersStart = match.Index + match.Length;
            var parametersSection = GetParametersSection(content, parametersStart);

            foreach (Match paramMatch in ParameterRegex().Matches(parametersSection))
            {
                var paramName = paramMatch.Groups["name"].Value;
                symbols.Add(new CodeSymbol(paramName, CodeSymbolKind.Parameter, procedureName));
            }
        }

        return symbols;
    }

    private static string GetParametersSection(string content, int startIndex)
    {
        // Parameters appear between the procedure name and the AS/BEGIN keyword.
        // We scan forward until we find AS or BEGIN on its own line.
        var remaining = content[startIndex..];
        var asMatch = AsOrBeginRegex().Match(remaining);

        if (asMatch.Success)
        {
            return remaining[..asMatch.Index];
        }

        // If no AS/BEGIN found, return remaining content (may be malformed SQL)
        return remaining;
    }

    private static List<string> ExtractComments(string content)
    {
        var comments = new List<string>();

        // Extract multi-line block comments: /* ... */
        foreach (Match match in BlockCommentRegex().Matches(content))
        {
            var rawComment = match.Groups["comment"].Value;
            var cleanedComment = CleanBlockComment(rawComment);

            if (!string.IsNullOrWhiteSpace(cleanedComment))
            {
                comments.Add(cleanedComment);
            }
        }

        // Extract single-line comments: -- ...
        foreach (Match match in SingleLineCommentRegex().Matches(content))
        {
            var commentText = match.Groups["comment"].Value.Trim();

            if (!string.IsNullOrWhiteSpace(commentText))
            {
                comments.Add(commentText);
            }
        }

        return comments;
    }

    private static string CleanBlockComment(string rawComment)
    {
        // Remove leading * and whitespace from each line of the block comment
        var lines = rawComment.Split('\n')
            .Select(line => line.TrimStart().TrimStart('*').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(" ", lines);
    }

    // Matches: CREATE [OR ALTER] PROCEDURE|PROC [schema.]ProcedureName
    [GeneratedRegex(
        @"CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+(?:\[?\w+\]?\.)?(?<name>\[?\w+\]?)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex StoredProcedureRegex();

    // Matches: @ParameterName datatype
    [GeneratedRegex(
        @"(?<name>@\w+)\s+\w+",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ParameterRegex();

    // Matches the AS or BEGIN keyword that terminates the parameter section
    [GeneratedRegex(
        @"^\s*\b(?:AS|BEGIN)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex AsOrBeginRegex();

    // Matches block comments: /* ... */
    [GeneratedRegex(
        @"/\*(?<comment>[\s\S]*?)\*/",
        RegexOptions.Multiline)]
    private static partial Regex BlockCommentRegex();

    // Matches single-line comments: -- ...
    [GeneratedRegex(
        @"--\s*(?<comment>.*)$",
        RegexOptions.Multiline)]
    private static partial Regex SingleLineCommentRegex();
}
