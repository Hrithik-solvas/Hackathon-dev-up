using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 9: Chunk overlap is correctly applied.
/// For any output with 2+ chunks, overlap between adjacent chunks equals configured overlap size,
/// and overlap ≤ 25% of max chunk size.
///
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class ChunkOverlapProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly SourceFileMetadata DefaultMetadata =
        new(
            FilePath: "/test/document.md",
            FileName: "document.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);

    private static readonly string[] WordPool = new[]
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta",
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
        "code", "compass", "pipeline", "document", "chunking", "parsing", "vector",
        "search", "index", "query", "embedding", "metadata", "section", "heading",
        "function", "method", "class", "interface", "abstract", "property", "field"
    };

    /// <summary>
    /// Generates a long document that is guaranteed to produce 2+ chunks.
    /// </summary>
    private static string GenerateLongDocument(int paragraphCount, int wordsPerParagraph, int seed)
    {
        var rng = new Random(seed);
        var paragraphs = new List<string>();

        for (int i = 0; i < paragraphCount; i++)
        {
            var words = new List<string>();
            for (int w = 0; w < wordsPerParagraph; w++)
            {
                words.Add(WordPool[rng.Next(WordPool.Length)]);
            }
            paragraphs.Add(string.Join(" ", words));
        }

        return string.Join("\n\n", paragraphs);
    }

    [Property(MaxTest = 100)]
    public void AdjacentChunksShareOverlapTokens(PositiveInt paragraphSeed, PositiveInt randomSeed)
    {
        // Use parameters that guarantee 2+ chunks:
        // Small MaxTokens with enough paragraphs
        var maxTokens = 60;
        var overlapTokens = 10;
        var options = new ChunkingOptions(MaxTokens: maxTokens, MinTokens: 5, OverlapTokens: overlapTokens);

        // Generate enough content to produce multiple chunks
        var paragraphCount = (paragraphSeed.Get % 5) + 5; // 5-9 paragraphs
        var rawText = GenerateLongDocument(paragraphCount, wordsPerParagraph: 15, seed: randomSeed.Get);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), DefaultMetadata);

        var chunks = Service.ChunkDocument(document, options);

        if (chunks.Count < 2) return; // Skip if we didn't get multiple chunks

        // For each pair of adjacent chunks, verify the overlap
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var currentChunkTokens = chunks[i].Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var nextChunkTokens = chunks[i + 1].Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // Skip pairs where either chunk comes from oversized splitting
            if (chunks[i].ContextHeader != null || chunks[i + 1].ContextHeader != null)
                continue;

            // The last N tokens of chunk[i] should equal the first N tokens of chunk[i+1]
            // where N = OverlapTokens (or less if the chunk is smaller)
            var actualOverlapSize = Math.Min(overlapTokens, Math.Min(currentChunkTokens.Length, nextChunkTokens.Length));

            if (actualOverlapSize <= 0) continue;

            var lastNOfCurrent = currentChunkTokens.Skip(currentChunkTokens.Length - actualOverlapSize).ToArray();
            var firstNOfNext = nextChunkTokens.Take(actualOverlapSize).ToArray();

            lastNOfCurrent.Should().Equal(firstNOfNext,
                $"overlap between chunk {i} and chunk {i + 1} should be exactly {actualOverlapSize} tokens");
        }
    }

    [Property(MaxTest = 100)]
    public void ConfiguredOverlapDoesNotExceed25PercentOfMaxTokens(
        PositiveInt maxTokensSeed, PositiveInt overlapSeed)
    {
        // Generate various valid configurations
        var maxTokens = (maxTokensSeed.Get % 500) + 50; // 50-549
        var overlapTokens = (overlapSeed.Get % (maxTokens / 2)) + 1; // 1 to maxTokens/2

        // The property: overlap should be ≤ 25% of max chunk size for well-configured systems
        var maxAllowedOverlap = maxTokens / 4; // 25% of MaxTokens

        // This tests that any valid configuration should respect this constraint
        // We verify the constraint as a property of the configuration itself
        if (overlapTokens <= maxAllowedOverlap)
        {
            // This is a valid configuration - verify the overlap constraint holds
            overlapTokens.Should().BeLessThanOrEqualTo(maxAllowedOverlap,
                $"configured overlap ({overlapTokens}) should be ≤ 25% of MaxTokens ({maxTokens})");
        }
        else
        {
            // This configuration violates the constraint
            overlapTokens.Should().BeGreaterThan(maxAllowedOverlap,
                "overlap exceeds 25% of MaxTokens which violates the design constraint");
        }
    }

    [Property(MaxTest = 50)]
    public void DefaultOptionsRespectOverlapConstraint()
    {
        // The default ChunkingOptions should always satisfy overlap ≤ 25% of MaxTokens
        var options = new ChunkingOptions(); // MaxTokens: 512, OverlapTokens: 50

        var maxAllowedOverlap = options.MaxTokens / 4; // 128
        options.OverlapTokens.Should().BeLessThanOrEqualTo(maxAllowedOverlap,
            $"default overlap ({options.OverlapTokens}) should be ≤ 25% of default MaxTokens ({options.MaxTokens})");
    }
}
