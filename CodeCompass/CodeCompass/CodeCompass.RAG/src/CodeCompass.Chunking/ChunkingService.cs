using System.Text.RegularExpressions;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Chunking;

/// <summary>
/// Splits parsed content into semantically coherent chunks respecting logical boundaries.
/// </summary>
public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    // Sentence boundary: period, exclamation, or question mark followed by whitespace or end of text
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[.!?])(?:\s+|$)", RegexOptions.Compiled);

    // Statement boundary: semicolon or closing brace followed by a newline
    private static readonly Regex StatementBoundaryRegex = new(@"(?<=[;}])\s*\n", RegexOptions.Compiled);

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<Chunk> ChunkDocument(ParsedDocument document, ChunkingOptions? options = null)
    {
        var opts = options ?? new ChunkingOptions();
        var text = document.RawText;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Document {FilePath} has no text content to chunk.", document.SourceMetadata.FilePath);
            return Array.Empty<Chunk>();
        }

        var paragraphs = SplitIntoParagraphs(text);
        var chunks = BuildChunksFromParagraphs(paragraphs, opts, document.SourceMetadata, document.Headings, text);

        _logger.LogDebug("Chunked document {FilePath} into {ChunkCount} chunks.", document.SourceMetadata.FilePath, chunks.Count);
        return chunks;
    }

    /// <inheritdoc />
    public IReadOnlyList<Chunk> ChunkCode(ParsedCode code, ChunkingOptions? options = null)
    {
        var opts = options ?? new ChunkingOptions();
        var text = code.RawText;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Code file {FilePath} has no text content to chunk.", code.SourceMetadata.FilePath);
            return Array.Empty<Chunk>();
        }

        var logicalUnits = ExtractLogicalUnits(text, code.Symbols);
        var chunks = BuildChunksFromCodeUnits(logicalUnits, opts, code.SourceMetadata);

        _logger.LogDebug("Chunked code file {FilePath} into {ChunkCount} chunks.", code.SourceMetadata.FilePath, chunks.Count);
        return chunks;
    }

    /// <summary>
    /// Extracts logical code units from the raw text based on symbol positions.
    /// If symbols are present, splits at class/method/function/procedure declarations.
    /// Otherwise, falls back to splitting at blank lines (line-based splitting).
    /// </summary>
    private static List<string> ExtractLogicalUnits(string text, IReadOnlyList<CodeSymbol> symbols)
    {
        if (symbols.Count == 0)
        {
            return SplitAtBlankLines(text);
        }

        return SplitAtSymbolBoundaries(text, symbols);
    }

    /// <summary>
    /// Splits code at symbol declaration boundaries by detecting symbol names in the source text.
    /// Each logical unit is the content from one symbol declaration up to (but not including) the next.
    /// </summary>
    private static List<string> SplitAtSymbolBoundaries(string text, IReadOnlyList<CodeSymbol> symbols)
    {
        var lines = text.Split('\n');
        var splitIndices = new List<int>();

        // Find line indices where symbol declarations appear
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            foreach (var symbol in symbols)
            {
                if (IsDeclarationLine(line, symbol))
                {
                    splitIndices.Add(i);
                    break;
                }
            }
        }

        // Remove duplicates and sort
        splitIndices = splitIndices.Distinct().OrderBy(x => x).ToList();

        if (splitIndices.Count == 0)
        {
            // No symbol declarations found in text, fall back to blank line splitting
            return SplitAtBlankLines(text);
        }

        var units = new List<string>();

        // If there's content before the first symbol, include it as a unit
        if (splitIndices[0] > 0)
        {
            var preamble = string.Join("\n", lines.Take(splitIndices[0])).Trim();
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                units.Add(preamble);
            }
        }

        // Each symbol boundary to the next forms a logical unit
        for (int i = 0; i < splitIndices.Count; i++)
        {
            int startLine = splitIndices[i];
            int endLine = (i + 1 < splitIndices.Count) ? splitIndices[i + 1] : lines.Length;
            var unit = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine)).Trim();
            if (!string.IsNullOrWhiteSpace(unit))
            {
                units.Add(unit);
            }
        }

        return units;
    }

    /// <summary>
    /// Checks if a line appears to be a declaration of the given symbol.
    /// Matches common patterns for class, method, function, procedure, component declarations.
    /// </summary>
    private static bool IsDeclarationLine(string trimmedLine, CodeSymbol symbol)
    {
        // Match based on symbol kind and name
        var name = symbol.Name;
        return symbol.Kind switch
        {
            CodeSymbolKind.Class => trimmedLine.Contains($"class {name}") ||
                                    trimmedLine.Contains($"interface {name}") ||
                                    trimmedLine.Contains($"struct {name}"),
            CodeSymbolKind.Method => trimmedLine.Contains($"{name}(") ||
                                     trimmedLine.Contains($"{name} ("),
            CodeSymbolKind.Component => trimmedLine.Contains($"function {name}") ||
                                        trimmedLine.Contains($"const {name}") ||
                                        trimmedLine.Contains($"export default function {name}") ||
                                        trimmedLine.Contains($"export function {name}"),
            CodeSymbolKind.Hook => trimmedLine.Contains($"function {name}") ||
                                    trimmedLine.Contains($"const {name}"),
            CodeSymbolKind.StoredProcedure => trimmedLine.Contains(name, StringComparison.OrdinalIgnoreCase) &&
                                              (trimmedLine.Contains("PROCEDURE", StringComparison.OrdinalIgnoreCase) ||
                                               trimmedLine.Contains("PROC", StringComparison.OrdinalIgnoreCase)),
            CodeSymbolKind.Parameter => false, // Parameters don't create split points
            _ => false
        };
    }

    /// <summary>
    /// Fallback: splits code at blank line boundaries (similar to paragraph splitting for documents).
    /// Groups consecutive non-blank lines into logical units.
    /// </summary>
    private static List<string> SplitAtBlankLines(string text)
    {
        var units = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        return units;
    }

    /// <summary>
    /// Builds chunks by greedily accumulating code units until the max token limit would be exceeded.
    /// Applies overlap from the end of the previous chunk when starting a new chunk.
    /// Merges small trailing content with the previous chunk if below min threshold.
    /// Handles oversized code units by splitting them at statement boundaries.
    /// </summary>
    private List<Chunk> BuildChunksFromCodeUnits(
        List<string> units,
        ChunkingOptions options,
        SourceFileMetadata sourceMetadata)
    {
        var chunks = new List<Chunk>();
        var currentTokens = new List<string>();
        int chunkIndex = 0;

        for (int i = 0; i < units.Count; i++)
        {
            var unitTokens = Tokenize(units[i]);

            // Check if this single unit exceeds max tokens (oversized)
            if (unitTokens.Count > options.MaxTokens)
            {
                // Finalize current chunk if it has content
                if (currentTokens.Count > 0)
                {
                    chunks.Add(CreateCodeChunk(currentTokens, chunkIndex, sourceMetadata));
                    chunkIndex++;
                    currentTokens = new List<string>();
                }

                // Extract the declaration signature (first line) as context header
                var contextHeader = ExtractDeclarationSignature(units[i]);

                // Split oversized code unit at statement boundaries
                var subChunks = SplitOversizedCodeUnit(units[i], options, chunkIndex, sourceMetadata, contextHeader);
                chunks.AddRange(subChunks);
                chunkIndex += subChunks.Count;
                continue;
            }

            // If adding this unit would exceed max, finalize the current chunk
            if (currentTokens.Count > 0 && currentTokens.Count + unitTokens.Count > options.MaxTokens)
            {
                // Finalize current chunk
                chunks.Add(CreateCodeChunk(currentTokens, chunkIndex, sourceMetadata));
                chunkIndex++;

                // Start new chunk with overlap from end of previous chunk
                var overlapTokens = GetOverlapTokens(currentTokens, options.OverlapTokens);
                currentTokens = new List<string>(overlapTokens);
            }

            currentTokens.AddRange(unitTokens);
        }

        // Handle remaining tokens
        if (currentTokens.Count > 0)
        {
            // If below min threshold and we have a previous chunk, merge with previous
            if (chunks.Count > 0 && CountNonOverlapTokens(currentTokens, options.OverlapTokens) < options.MinTokens)
            {
                // Merge with the previous chunk
                var previousChunk = chunks[^1];
                var previousTokens = Tokenize(previousChunk.Text);
                // Add only the non-overlap portion to the previous chunk
                var nonOverlapTokens = currentTokens.Skip(Math.Min(options.OverlapTokens, currentTokens.Count)).ToList();
                if (nonOverlapTokens.Count > 0)
                {
                    previousTokens.AddRange(nonOverlapTokens);
                    chunks[^1] = CreateCodeChunk(previousTokens, previousChunk.Index, sourceMetadata);
                }
            }
            else
            {
                chunks.Add(CreateCodeChunk(currentTokens, chunkIndex, sourceMetadata));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Creates a Chunk from the given tokens with code-specific metadata.
    /// </summary>
    private static Chunk CreateCodeChunk(List<string> tokens, int index, SourceFileMetadata sourceMetadata)
    {
        var text = string.Join(" ", tokens);
        var metadata = new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: index,
            ContentType: "code",
            Language: GetLanguageFromExtension(sourceMetadata.FileExtension),
            LastModified: sourceMetadata.LastModified,
            SectionHeading: null);

        return new Chunk(
            Text: text,
            Index: index,
            ContextHeader: null,
            Metadata: metadata);
    }

    /// <summary>
    /// Maps file extension to language name.
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
    /// Splits text into paragraphs at double-newline boundaries.
    /// </summary>
    private static List<string> SplitIntoParagraphs(string text)
    {
        // Split on double newline (with possible whitespace between)
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return paragraphs;
    }

    /// <summary>
    /// Builds chunks by greedily accumulating paragraphs until the max token limit would be exceeded.
    /// Applies overlap from the end of the previous chunk when starting a new chunk.
    /// Merges small trailing content with the previous chunk if below min threshold.
    /// Handles oversized paragraphs by splitting them at sentence boundaries.
    /// </summary>
    private List<Chunk> BuildChunksFromParagraphs(
        List<string> paragraphs,
        ChunkingOptions options,
        SourceFileMetadata sourceMetadata,
        IReadOnlyList<Heading> headings,
        string fullText)
    {
        var chunks = new List<Chunk>();
        var currentTokens = new List<string>();
        int chunkIndex = 0;
        int textPositionSoFar = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraphTokens = Tokenize(paragraphs[i]);

            // Check if this single paragraph exceeds max tokens (oversized)
            if (paragraphTokens.Count > options.MaxTokens)
            {
                // Finalize current chunk if it has content
                if (currentTokens.Count > 0)
                {
                    chunks.Add(CreateChunk(currentTokens, chunkIndex, sourceMetadata));
                    chunkIndex++;
                    currentTokens = new List<string>();
                }

                // Find the nearest ancestor heading for context
                var paragraphPosition = fullText.IndexOf(paragraphs[i], textPositionSoFar, StringComparison.Ordinal);
                var contextHeader = FindNearestHeading(headings, fullText, paragraphPosition);

                // Split oversized paragraph at sentence boundaries
                var subChunks = SplitOversizedParagraph(paragraphs[i], options, chunkIndex, sourceMetadata, contextHeader);
                chunks.AddRange(subChunks);
                chunkIndex += subChunks.Count;

                // Update text position tracking
                if (paragraphPosition >= 0)
                {
                    textPositionSoFar = paragraphPosition + paragraphs[i].Length;
                }
                continue;
            }

            // If adding this paragraph would exceed max, finalize the current chunk
            if (currentTokens.Count > 0 && currentTokens.Count + paragraphTokens.Count > options.MaxTokens)
            {
                // Finalize current chunk
                chunks.Add(CreateChunk(currentTokens, chunkIndex, sourceMetadata));
                chunkIndex++;

                // Start new chunk with overlap from end of previous chunk
                var overlapTokens = GetOverlapTokens(currentTokens, options.OverlapTokens);
                currentTokens = new List<string>(overlapTokens);
            }

            currentTokens.AddRange(paragraphTokens);

            // Track text position
            var pos = fullText.IndexOf(paragraphs[i], textPositionSoFar, StringComparison.Ordinal);
            if (pos >= 0)
            {
                textPositionSoFar = pos + paragraphs[i].Length;
            }
        }

        // Handle remaining tokens
        if (currentTokens.Count > 0)
        {
            // If below min threshold and we have a previous chunk, merge with previous
            if (chunks.Count > 0 && CountNonOverlapTokens(currentTokens, options.OverlapTokens) < options.MinTokens)
            {
                // Merge with the previous chunk
                var previousChunk = chunks[^1];
                var previousTokens = Tokenize(previousChunk.Text);
                // Add only the non-overlap portion to the previous chunk
                var nonOverlapTokens = currentTokens.Skip(Math.Min(options.OverlapTokens, currentTokens.Count)).ToList();
                if (nonOverlapTokens.Count > 0)
                {
                    previousTokens.AddRange(nonOverlapTokens);
                    chunks[^1] = CreateChunk(previousTokens, previousChunk.Index, sourceMetadata);
                }
            }
            else
            {
                chunks.Add(CreateChunk(currentTokens, chunkIndex, sourceMetadata));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Counts tokens excluding the overlap portion at the start.
    /// </summary>
    private static int CountNonOverlapTokens(List<string> tokens, int overlapSize)
    {
        return Math.Max(0, tokens.Count - overlapSize);
    }

    /// <summary>
    /// Gets the overlap tokens from the end of the given token list.
    /// </summary>
    private static List<string> GetOverlapTokens(List<string> tokens, int overlapSize)
    {
        if (overlapSize <= 0 || tokens.Count == 0)
            return new List<string>();

        int startIndex = Math.Max(0, tokens.Count - overlapSize);
        return tokens.Skip(startIndex).ToList();
    }

    /// <summary>
    /// Creates a Chunk from the given tokens.
    /// </summary>
    private static Chunk CreateChunk(List<string> tokens, int index, SourceFileMetadata sourceMetadata)
    {
        var text = string.Join(" ", tokens);
        var metadata = new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: index,
            ContentType: "document",
            Language: null,
            LastModified: sourceMetadata.LastModified,
            SectionHeading: null);

        return new Chunk(
            Text: text,
            Index: index,
            ContextHeader: null,
            Metadata: metadata);
    }

    /// <summary>
    /// Splits an oversized paragraph at sentence boundaries into sub-chunks that respect the max token limit.
    /// Each sub-chunk gets the provided context header.
    /// </summary>
    private List<Chunk> SplitOversizedParagraph(
        string paragraph,
        ChunkingOptions options,
        int startIndex,
        SourceFileMetadata sourceMetadata,
        string? contextHeader)
    {
        var sentences = SplitAtSentenceBoundaries(paragraph);
        var chunks = new List<Chunk>();
        var currentTokens = new List<string>();
        int chunkIndex = startIndex;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = Tokenize(sentence);
            if (sentenceTokens.Count == 0) continue;

            // If adding this sentence would exceed max, finalize current chunk
            if (currentTokens.Count > 0 && currentTokens.Count + sentenceTokens.Count > options.MaxTokens)
            {
                chunks.Add(CreateChunkWithContextHeader(currentTokens, chunkIndex, sourceMetadata, contextHeader));
                chunkIndex++;

                // Start new chunk with overlap
                var overlapTokens = GetOverlapTokens(currentTokens, options.OverlapTokens);
                currentTokens = new List<string>(overlapTokens);
            }

            currentTokens.AddRange(sentenceTokens);
        }

        // Add remaining content
        if (currentTokens.Count > 0)
        {
            chunks.Add(CreateChunkWithContextHeader(currentTokens, chunkIndex, sourceMetadata, contextHeader));
        }

        _logger.LogDebug("Split oversized paragraph into {Count} sub-chunks with context header: {Header}",
            chunks.Count, contextHeader ?? "(none)");

        return chunks;
    }

    /// <summary>
    /// Splits an oversized code unit at statement boundaries (semicolons or closing braces followed by newline)
    /// into sub-chunks that respect the max token limit. Each sub-chunk gets the declaration signature as context header.
    /// </summary>
    private List<Chunk> SplitOversizedCodeUnit(
        string unit,
        ChunkingOptions options,
        int startIndex,
        SourceFileMetadata sourceMetadata,
        string? contextHeader)
    {
        var statements = SplitAtStatementBoundaries(unit);
        var chunks = new List<Chunk>();
        var currentTokens = new List<string>();
        int chunkIndex = startIndex;

        foreach (var statement in statements)
        {
            var statementTokens = Tokenize(statement);
            if (statementTokens.Count == 0) continue;

            // If adding this statement would exceed max, finalize current chunk
            if (currentTokens.Count > 0 && currentTokens.Count + statementTokens.Count > options.MaxTokens)
            {
                chunks.Add(CreateCodeChunkWithContextHeader(currentTokens, chunkIndex, sourceMetadata, contextHeader));
                chunkIndex++;

                // Start new chunk with overlap
                var overlapTokens = GetOverlapTokens(currentTokens, options.OverlapTokens);
                currentTokens = new List<string>(overlapTokens);
            }

            currentTokens.AddRange(statementTokens);
        }

        // Add remaining content
        if (currentTokens.Count > 0)
        {
            chunks.Add(CreateCodeChunkWithContextHeader(currentTokens, chunkIndex, sourceMetadata, contextHeader));
        }

        _logger.LogDebug("Split oversized code unit into {Count} sub-chunks with context header: {Header}",
            chunks.Count, contextHeader ?? "(none)");

        return chunks;
    }

    /// <summary>
    /// Splits text at sentence boundaries (., !, ? followed by whitespace or end-of-text).
    /// </summary>
    internal static List<string> SplitAtSentenceBoundaries(string text)
    {
        var parts = SentenceBoundaryRegex.Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return parts.Count > 0 ? parts : new List<string> { text };
    }

    /// <summary>
    /// Splits code at statement boundaries (semicolons or closing braces followed by newline).
    /// </summary>
    internal static List<string> SplitAtStatementBoundaries(string text)
    {
        var parts = StatementBoundaryRegex.Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return parts.Count > 0 ? parts : new List<string> { text };
    }

    /// <summary>
    /// Finds the nearest ancestor heading that precedes the given text position.
    /// Returns the heading text or null if no heading is found.
    /// </summary>
    internal static string? FindNearestHeading(IReadOnlyList<Heading> headings, string fullText, int position)
    {
        if (headings.Count == 0 || position < 0)
            return null;

        // Find the heading that appears closest before the given position
        string? nearestHeading = null;
        int nearestPosition = -1;

        foreach (var heading in headings)
        {
            // Search for the heading text in the full document
            var headingIndex = fullText.IndexOf(heading.Text, StringComparison.Ordinal);
            if (headingIndex >= 0 && headingIndex < position && headingIndex > nearestPosition)
            {
                nearestPosition = headingIndex;
                nearestHeading = heading.Text;
            }
        }

        return nearestHeading;
    }

    /// <summary>
    /// Extracts the declaration signature (first line) from a code unit.
    /// This is typically the class/method/function/procedure declaration line.
    /// </summary>
    internal static string? ExtractDeclarationSignature(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return null;

        var firstNewline = unit.IndexOf('\n');
        var firstLine = firstNewline >= 0 ? unit[..firstNewline].Trim() : unit.Trim();

        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }

    /// <summary>
    /// Creates a Chunk with a context header from the given tokens.
    /// </summary>
    private static Chunk CreateChunkWithContextHeader(
        List<string> tokens,
        int index,
        SourceFileMetadata sourceMetadata,
        string? contextHeader)
    {
        var text = string.Join(" ", tokens);
        var metadata = new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: index,
            ContentType: "document",
            Language: null,
            LastModified: sourceMetadata.LastModified,
            SectionHeading: contextHeader);

        return new Chunk(
            Text: text,
            Index: index,
            ContextHeader: contextHeader,
            Metadata: metadata);
    }

    /// <summary>
    /// Creates a code Chunk with a context header from the given tokens.
    /// </summary>
    private static Chunk CreateCodeChunkWithContextHeader(
        List<string> tokens,
        int index,
        SourceFileMetadata sourceMetadata,
        string? contextHeader)
    {
        var text = string.Join(" ", tokens);
        var metadata = new ChunkMetadata(
            SourceFilePath: sourceMetadata.FilePath,
            ChunkIndex: index,
            ContentType: "code",
            Language: GetLanguageFromExtension(sourceMetadata.FileExtension),
            LastModified: sourceMetadata.LastModified,
            SectionHeading: contextHeader);

        return new Chunk(
            Text: text,
            Index: index,
            ContextHeader: contextHeader,
            Metadata: metadata);
    }

    /// <summary>
    /// Simple whitespace-based tokenization. Delegates to <see cref="TokenCounter"/>.
    /// </summary>
    internal static List<string> Tokenize(string text)
    {
        return TokenCounter.Tokenize(text).ToList();
    }

    /// <summary>
    /// Counts tokens in a text using whitespace-based splitting. Delegates to <see cref="TokenCounter"/>.
    /// </summary>
    internal static int CountTokens(string text)
    {
        return TokenCounter.CountTokens(text);
    }
}
