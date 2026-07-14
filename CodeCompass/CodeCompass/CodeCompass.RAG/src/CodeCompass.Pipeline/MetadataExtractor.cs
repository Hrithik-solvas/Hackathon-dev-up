using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Pipeline;

/// <summary>
/// Extracts and enriches metadata from parsed content, producing ChunkMetadata objects
/// conforming to the VectorStore schema.
/// </summary>
public class MetadataExtractor : IMetadataExtractor
{
    private readonly ILogger<MetadataExtractor> _logger;

    public MetadataExtractor(ILogger<MetadataExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ChunkMetadata ExtractDocumentMetadata(ParsedDocument document, int chunkIndex, string? nearestHeading)
    {
        var sourceMetadata = document.SourceMetadata;

        _logger.LogDebug(
            "Extracting document metadata for chunk {ChunkIndex} of {FilePath}, heading: {Heading}",
            chunkIndex, sourceMetadata.FilePath, nearestHeading ?? "(none)");

        return new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: chunkIndex,
            ContentType: "document",
            Language: null,
            LastModified: sourceMetadata.LastModified,
            SectionHeading: nearestHeading);
    }

    /// <inheritdoc />
    public ChunkMetadata ExtractCodeMetadata(ParsedCode code, int chunkIndex, string? containingSymbol)
    {
        var sourceMetadata = code.SourceMetadata;
        var language = GetLanguageFromExtension(sourceMetadata.FileExtension);

        // If no containingSymbol was explicitly provided, resolve it from the code's symbols
        var resolvedSymbol = containingSymbol ?? ResolveContainingSymbol(code, chunkIndex);

        _logger.LogDebug(
            "Extracting code metadata for chunk {ChunkIndex} of {FilePath}, language: {Language}, symbol: {Symbol}",
            chunkIndex, sourceMetadata.FilePath, language ?? "(unknown)", resolvedSymbol ?? "(none)");

        return new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: chunkIndex,
            ContentType: "code",
            Language: language,
            LastModified: sourceMetadata.LastModified,
            SectionHeading: resolvedSymbol);
    }

    /// <summary>
    /// Resolves the containing symbol hierarchy for a given chunk from the parsed code's symbol list.
    /// Builds a qualified name like "Namespace.ClassName.MethodName" based on the symbol relationships.
    /// </summary>
    /// <param name="code">The parsed code containing symbols.</param>
    /// <param name="chunkIndex">The zero-based chunk index used to determine which symbol the chunk belongs to.</param>
    /// <returns>
    /// A dot-separated qualified symbol name (e.g., "MyNamespace.MyClass.MyMethod"),
    /// or null if no symbols are available.
    /// </returns>
    public static string? ResolveContainingSymbol(ParsedCode code, int chunkIndex)
    {
        if (code.Symbols.Count == 0)
            return null;

        // Strategy: Use the chunk index to find the most relevant symbol.
        // For code files, chunks typically correspond to logical units (classes, methods).
        // We pick the symbol at or near the chunk index (clamped to available symbols),
        // then build its full qualified name by walking up the parent chain.

        // Find the best matching symbol for this chunk index.
        // Methods and classes are ordered as they appear in the file.
        // We attempt to find a method-level symbol first, then fall back to a class.
        var methods = code.Symbols
            .Where(s => s.Kind == CodeSymbolKind.Method || s.Kind == CodeSymbolKind.Hook ||
                        s.Kind == CodeSymbolKind.StoredProcedure || s.Kind == CodeSymbolKind.Component)
            .ToList();

        var classes = code.Symbols
            .Where(s => s.Kind == CodeSymbolKind.Class)
            .ToList();

        // Try to find a method-level symbol matching the chunk index
        if (methods.Count > 0 && chunkIndex < methods.Count)
        {
            var method = methods[chunkIndex];
            return BuildQualifiedName(code.Symbols, method);
        }

        // If chunk index exceeds method count, try to match against all symbols
        if (methods.Count > 0)
        {
            // Use the last method available (chunk is beyond the methods)
            var method = methods[^1];
            return BuildQualifiedName(code.Symbols, method);
        }

        // Fall back to class-level symbol
        if (classes.Count > 0)
        {
            var classIndex = Math.Min(chunkIndex, classes.Count - 1);
            var classSymbol = classes[classIndex];
            return BuildQualifiedName(code.Symbols, classSymbol);
        }

        // Last resort: use the first available symbol
        return BuildQualifiedName(code.Symbols, code.Symbols[0]);
    }

    /// <summary>
    /// Builds a fully qualified name for a symbol by walking up its parent chain.
    /// For example, a method "DoWork" with parent "MyClass" in namespace "MyNamespace"
    /// would produce "MyNamespace.MyClass.DoWork".
    /// </summary>
    private static string BuildQualifiedName(IReadOnlyList<CodeSymbol> allSymbols, CodeSymbol symbol)
    {
        var parts = new List<string> { symbol.Name };

        var current = symbol;
        while (!string.IsNullOrEmpty(current.ParentName))
        {
            var parentName = current.ParentName;
            parts.Insert(0, parentName);

            // Try to find the parent symbol to continue walking up the chain
            var parentSymbol = allSymbols.FirstOrDefault(s => s.Name == parentName);
            if (parentSymbol == null)
                break;

            current = parentSymbol;
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// Resolves the nearest ancestor heading for a chunk at a given position within a document.
    /// Builds a heading hierarchy and returns the full heading path for the chunk's position.
    /// </summary>
    /// <param name="headings">The list of headings extracted from the document.</param>
    /// <param name="fullText">The full text of the document.</param>
    /// <param name="chunkStartPosition">The character position where the chunk starts in the full text.</param>
    /// <returns>The nearest ancestor heading text, or null if no heading precedes the chunk.</returns>
    public static string? ResolveNearestAncestorHeading(
        IReadOnlyList<Heading> headings,
        string fullText,
        int chunkStartPosition)
    {
        if (headings.Count == 0 || string.IsNullOrEmpty(fullText) || chunkStartPosition < 0)
            return null;

        // Find the positions of all headings in the document text
        var headingPositions = new List<(Heading Heading, int Position)>();

        int searchStart = 0;
        foreach (var heading in headings)
        {
            var position = fullText.IndexOf(heading.Text, searchStart, StringComparison.Ordinal);
            if (position >= 0)
            {
                headingPositions.Add((heading, position));
                searchStart = position + heading.Text.Length;
            }
            else
            {
                // Try searching from the beginning as fallback for out-of-order headings
                position = fullText.IndexOf(heading.Text, StringComparison.Ordinal);
                if (position >= 0)
                {
                    headingPositions.Add((heading, position));
                }
            }
        }

        if (headingPositions.Count == 0)
            return null;

        // Sort by position to ensure proper ordering
        headingPositions.Sort((a, b) => a.Position.CompareTo(b.Position));

        // Find all headings that precede the chunk's start position
        var precedingHeadings = headingPositions
            .Where(hp => hp.Position < chunkStartPosition)
            .ToList();

        if (precedingHeadings.Count == 0)
            return null;

        // Build the ancestor hierarchy from preceding headings
        // Walk through preceding headings and maintain a stack representing the current heading hierarchy
        var headingStack = new List<Heading>();

        foreach (var (heading, _) in precedingHeadings)
        {
            // Remove headings at same level or lower (higher number = deeper nesting)
            // When we encounter a heading at level N, remove all headings at level >= N
            while (headingStack.Count > 0 && headingStack[^1].Level >= heading.Level)
            {
                headingStack.RemoveAt(headingStack.Count - 1);
            }

            headingStack.Add(heading);
        }

        if (headingStack.Count == 0)
            return null;

        // Return the full heading hierarchy path (e.g., "Chapter 1 > Section 1.1 > Subsection")
        return string.Join(" > ", headingStack.Select(h => h.Text));
    }

    /// <summary>
    /// Resolves heading hierarchy for all chunks produced from a document, assigning the nearest
    /// ancestor heading to each chunk based on its position within the document text.
    /// </summary>
    /// <param name="document">The parsed document containing headings and raw text.</param>
    /// <param name="chunks">The chunks produced by the chunking service.</param>
    /// <returns>
    /// A list of section headings corresponding to each chunk (by index).
    /// Each entry is the nearest ancestor heading text, or null if no heading precedes that chunk.
    /// </returns>
    public static IReadOnlyList<string?> ResolveHeadingsForChunks(
        ParsedDocument document,
        IReadOnlyList<Chunk> chunks)
    {
        if (document.Headings.Count == 0 || chunks.Count == 0)
        {
            return chunks.Select(_ => (string?)null).ToList();
        }

        var fullText = document.RawText;
        var results = new List<string?>();

        foreach (var chunk in chunks)
        {
            // Find the chunk's position in the full text
            var chunkPosition = fullText.IndexOf(chunk.Text, StringComparison.Ordinal);

            if (chunkPosition < 0)
            {
                // If exact match not found (e.g., due to tokenization changes), try first few words
                var firstWords = GetFirstWords(chunk.Text, 5);
                chunkPosition = fullText.IndexOf(firstWords, StringComparison.Ordinal);
            }

            var heading = ResolveNearestAncestorHeading(document.Headings, fullText, chunkPosition);
            results.Add(heading);
        }

        return results;
    }

    /// <summary>
    /// Maps file extension to a language identifier.
    /// </summary>
    private static string? GetLanguageFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".jsx" => "javascript",
            ".tsx" => "typescript",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".sql" => "sql",
            ".asp" => "asp",
            _ => null
        };
    }

    /// <summary>
    /// Gets the first N whitespace-separated words from a text.
    /// </summary>
    private static string GetFirstWords(string text, int count)
    {
        var words = text.Split(default(char[]), count + 1, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(count));
    }
}
