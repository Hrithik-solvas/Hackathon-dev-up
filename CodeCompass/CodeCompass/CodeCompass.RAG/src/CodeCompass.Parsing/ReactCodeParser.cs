using System.Text.RegularExpressions;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses JSX/TSX source files using regex-based pattern matching, extracting
/// React components, custom hooks, and JSDoc comment blocks.
/// </summary>
public partial class ReactCodeParser : ICodeParser
{
    private readonly ILogger<ReactCodeParser> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jsx",
        ".tsx"
    };

    public ReactCodeParser(ILogger<ReactCodeParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return SupportedExtensions.Contains(fileExtension);
    }

    /// <inheritdoc />
    public async Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Parsing React file: {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var symbols = ExtractSymbols(content);
        var documentationComments = ExtractJsDocComments(content);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        _logger.LogDebug(
            "Parsed React file {FilePath}: {SymbolCount} symbols, {CommentCount} documentation comments found",
            filePath, symbols.Count, documentationComments.Count);

        return new ParsedCode(content, symbols, documentationComments, sourceMetadata);
    }

    private static List<CodeSymbol> ExtractSymbols(string content)
    {
        var symbols = new List<CodeSymbol>();

        // Extract function declaration components: function ComponentName(...)
        foreach (Match match in FunctionComponentRegex().Matches(content))
        {
            var name = match.Groups["name"].Value;
            var kind = IsHookName(name) ? CodeSymbolKind.Hook : CodeSymbolKind.Component;
            symbols.Add(new CodeSymbol(name, kind, null));
        }

        // Extract arrow function / const components: const ComponentName = (...) => or const ComponentName = function
        foreach (Match match in ArrowFunctionComponentRegex().Matches(content))
        {
            var name = match.Groups["name"].Value;
            // Avoid duplicates if somehow matched by both patterns
            if (symbols.Any(s => s.Name == name))
                continue;

            var kind = IsHookName(name) ? CodeSymbolKind.Hook : CodeSymbolKind.Component;
            symbols.Add(new CodeSymbol(name, kind, null));
        }

        return symbols;
    }

    private static List<string> ExtractJsDocComments(string content)
    {
        var comments = new List<string>();

        foreach (Match match in JsDocCommentRegex().Matches(content))
        {
            var rawComment = match.Groups["comment"].Value;
            var cleanedComment = CleanJsDocComment(rawComment);

            if (!string.IsNullOrWhiteSpace(cleanedComment))
            {
                comments.Add(cleanedComment);
            }
        }

        return comments;
    }

    private static string CleanJsDocComment(string rawComment)
    {
        // Remove leading * and whitespace from each line of the JSDoc block
        var lines = rawComment.Split('\n')
            .Select(line => line.TrimStart().TrimStart('*').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(" ", lines);
    }

    private static bool IsHookName(string name)
    {
        // Custom hooks start with "use" followed by an uppercase letter
        return name.Length > 3 && name.StartsWith("use", StringComparison.Ordinal)
            && char.IsUpper(name[3]);
    }

    // Matches: [export [default]] function ComponentName(
    // Also matches: [export [default]] async function ComponentName(
    [GeneratedRegex(
        @"(?:export\s+(?:default\s+)?)?(?:async\s+)?function\s+(?<name>[A-Z]\w*|use[A-Z]\w*)\s*\(",
        RegexOptions.Multiline)]
    private static partial Regex FunctionComponentRegex();

    // Matches: [export] const ComponentName = (...) => or [export] const ComponentName = function
    // Also matches: [export] const ComponentName = React.memo(...), React.forwardRef(...)
    [GeneratedRegex(
        @"(?:export\s+(?:default\s+)?)?(?:const|let|var)\s+(?<name>[A-Z]\w*|use[A-Z]\w*)\s*=\s*(?:(?:async\s+)?\(|(?:async\s+)?(?:\w+)\s*=>|React\.(?:memo|forwardRef)\s*\(|function)",
        RegexOptions.Multiline)]
    private static partial Regex ArrowFunctionComponentRegex();

    // Matches JSDoc comment blocks: /** ... */
    [GeneratedRegex(
        @"/\*\*(?<comment>[\s\S]*?)\*/",
        RegexOptions.Multiline)]
    private static partial Regex JsDocCommentRegex();
}
