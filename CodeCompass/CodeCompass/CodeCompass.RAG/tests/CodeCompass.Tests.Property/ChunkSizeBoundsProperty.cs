using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 8: Chunk size bounds are respected.
/// For any input and valid chunking config, every chunk has token count ≥ min and ≤ max.
/// Exception: the last chunk and chunks from oversized splitting may have fewer tokens than min.
///
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class ChunkSizeBoundsProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly SourceFileMetadata DefaultDocMetadata =
        new(
            FilePath: "/test/document.md",
            FileName: "document.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);

    private static readonly SourceFileMetadata DefaultCodeMetadata =
        new(
            FilePath: "/test/code.cs",
            FileName: "code.cs",
            FileExtension: ".cs",
            LastModified: DateTimeOffset.UtcNow);

    private static readonly string[] WordPool = new[]
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta",
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
        "code", "compass", "pipeline", "document", "chunking", "parsing", "vector"
    };

    /// <summary>
    /// Generates a document with multiple paragraphs of various sizes.
    /// </summary>
    private static string GenerateDocument(int paragraphCount, int maxWordsPerParagraph, int seed)
    {
        var rng = new Random(seed);
        var paragraphs = new List<string>();

        for (int i = 0; i < paragraphCount; i++)
        {
            var wordCount = rng.Next(5, maxWordsPerParagraph + 1);
            var words = new List<string>();
            for (int w = 0; w < wordCount; w++)
            {
                words.Add(WordPool[rng.Next(WordPool.Length)]);
            }
            paragraphs.Add(string.Join(" ", words));
        }

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>
    /// Generates code with multiple methods.
    /// </summary>
    private static (string rawText, List<CodeSymbol> symbols) GenerateCode(int methodCount, int seed)
    {
        var rng = new Random(seed);
        var symbols = new List<CodeSymbol>();
        var lines = new List<string>();

        for (int i = 0; i < methodCount; i++)
        {
            var methodName = $"Method{i}";
            symbols.Add(new CodeSymbol(methodName, CodeSymbolKind.Method, "TestClass"));

            var bodyLines = new List<string>();
            bodyLines.Add($"public void {methodName}()");
            bodyLines.Add("{");
            var stmts = rng.Next(2, 8);
            for (int s = 0; s < stmts; s++)
            {
                bodyLines.Add($"    var val{s} = {rng.Next(1, 1000)};");
            }
            bodyLines.Add("}");
            lines.Add(string.Join("\n", bodyLines));
        }

        return (string.Join("\n\n", lines), symbols);
    }

    [Property(MaxTest = 100)]
    public void DocumentChunks_RespectMaxTokenBound(PositiveInt paragraphSeed, PositiveInt randomSeed)
    {
        var paragraphCount = (paragraphSeed.Get % 8) + 3; // 3-10 paragraphs
        var options = new ChunkingOptions(MaxTokens: 100, MinTokens: 10, OverlapTokens: 15);

        var rawText = GenerateDocument(paragraphCount, maxWordsPerParagraph: 30, seed: randomSeed.Get);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), DefaultDocMetadata);

        var chunks = Service.ChunkDocument(document, options);

        if (chunks.Count == 0) return;

        // Every chunk must respect max token bound.
        // Tolerance: overlap tokens may be prepended to a chunk, so the effective max is MaxTokens + OverlapTokens.
        var effectiveMax = options.MaxTokens + options.OverlapTokens;
        foreach (var chunk in chunks)
        {
            var tokenCount = TokenCounter.CountTokens(chunk.Text);
            tokenCount.Should().BeLessThanOrEqualTo(effectiveMax,
                $"chunk index {chunk.Index} has {tokenCount} tokens which exceeds effective max ({options.MaxTokens} + {options.OverlapTokens} overlap tolerance)");
        }
    }

    [Property(MaxTest = 100)]
    public void DocumentChunks_RespectMinTokenBound_ExceptLastChunk(PositiveInt paragraphSeed, PositiveInt randomSeed)
    {
        var paragraphCount = (paragraphSeed.Get % 8) + 3; // 3-10 paragraphs
        var options = new ChunkingOptions(MaxTokens: 100, MinTokens: 10, OverlapTokens: 15);

        var rawText = GenerateDocument(paragraphCount, maxWordsPerParagraph: 30, seed: randomSeed.Get);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), DefaultDocMetadata);

        var chunks = Service.ChunkDocument(document, options);

        if (chunks.Count <= 1) return;

        // All chunks except the last should respect min token bound
        // (unless they come from oversized splitting, indicated by non-null ContextHeader)
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var chunk = chunks[i];
            var tokenCount = TokenCounter.CountTokens(chunk.Text);

            // Chunks from oversized splitting may be smaller than min
            if (chunk.ContextHeader != null)
                continue;

            tokenCount.Should().BeGreaterThanOrEqualTo(options.MinTokens,
                $"non-last chunk index {chunk.Index} has {tokenCount} tokens which is below MinTokens={options.MinTokens}");
        }
    }

    [Property(MaxTest = 100)]
    public void CodeChunks_RespectMaxTokenBound(PositiveInt methodSeed, PositiveInt randomSeed)
    {
        var methodCount = (methodSeed.Get % 6) + 3; // 3-8 methods
        var options = new ChunkingOptions(MaxTokens: 100, MinTokens: 10, OverlapTokens: 15);

        var (rawText, symbols) = GenerateCode(methodCount, seed: randomSeed.Get);
        var parsedCode = new ParsedCode(rawText, symbols, Array.Empty<string>(), DefaultCodeMetadata);

        var chunks = Service.ChunkCode(parsedCode, options);

        if (chunks.Count == 0) return;

        // Every chunk must respect max token bound.
        // Tolerance: overlap tokens may be prepended to a chunk, so the effective max is MaxTokens + OverlapTokens.
        var effectiveMax = options.MaxTokens + options.OverlapTokens;
        foreach (var chunk in chunks)
        {
            var tokenCount = TokenCounter.CountTokens(chunk.Text);
            tokenCount.Should().BeLessThanOrEqualTo(effectiveMax,
                $"code chunk index {chunk.Index} has {tokenCount} tokens which exceeds effective max ({options.MaxTokens} + {options.OverlapTokens} overlap tolerance)");
        }
    }

    [Property(MaxTest = 100)]
    public void CodeChunks_RespectMinTokenBound_ExceptLastChunk(PositiveInt methodSeed, PositiveInt randomSeed)
    {
        var methodCount = (methodSeed.Get % 6) + 3; // 3-8 methods
        var options = new ChunkingOptions(MaxTokens: 100, MinTokens: 10, OverlapTokens: 15);

        var (rawText, symbols) = GenerateCode(methodCount, seed: randomSeed.Get);
        var parsedCode = new ParsedCode(rawText, symbols, Array.Empty<string>(), DefaultCodeMetadata);

        var chunks = Service.ChunkCode(parsedCode, options);

        if (chunks.Count <= 1) return;

        // All chunks except the last should respect min token bound
        // (unless they come from oversized splitting, indicated by non-null ContextHeader)
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var chunk = chunks[i];
            var tokenCount = TokenCounter.CountTokens(chunk.Text);

            // Chunks from oversized splitting may be smaller than min
            if (chunk.ContextHeader != null)
                continue;

            tokenCount.Should().BeGreaterThanOrEqualTo(options.MinTokens,
                $"non-last code chunk index {chunk.Index} has {tokenCount} tokens which is below MinTokens={options.MinTokens}");
        }
    }
}
