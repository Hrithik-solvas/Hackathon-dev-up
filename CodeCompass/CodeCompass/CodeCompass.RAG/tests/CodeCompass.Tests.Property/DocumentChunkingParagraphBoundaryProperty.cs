using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 6: Document chunking respects paragraph boundaries.
/// For any parsed document where individual paragraphs fit within the maximum chunk size,
/// no chunk boundary shall fall within a paragraph—each paragraph appears entirely within a single chunk.
///
/// **Validates: Requirements 3.1**
/// </summary>
public class DocumentChunkingParagraphBoundaryProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly SourceFileMetadata DefaultMetadata =
        new(
            FilePath: "/test/document.md",
            FileName: "document.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);

    /// <summary>
    /// Generates a list of random words (1-10 words each 3-8 chars) to form a paragraph,
    /// guaranteeing the paragraph token count is below maxTokens.
    /// </summary>
    private static List<string> GenerateParagraphs(int paragraphCount, int maxWordsPerParagraph, int seed)
    {
        var rng = new Random(seed);
        var paragraphs = new List<string>();
        var wordPool = new[]
        {
            "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta",
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
            "code", "compass", "pipeline", "document", "chunking", "parsing", "vector",
            "search", "index", "query", "embedding", "metadata", "section", "heading"
        };

        for (int i = 0; i < paragraphCount; i++)
        {
            var wordCount = rng.Next(1, maxWordsPerParagraph + 1);
            var words = new List<string>();
            for (int w = 0; w < wordCount; w++)
            {
                words.Add(wordPool[rng.Next(wordPool.Length)]);
            }
            paragraphs.Add(string.Join(" ", words));
        }

        return paragraphs;
    }

    [Property(MaxTest = 100)]
    public void EachParagraphAppearsEntirelyWithinASingleChunk_WhenParagraphsFitInMaxTokens(
        PositiveInt paragraphCountSeed, PositiveInt wordsSeed, PositiveInt randomSeed)
    {
        // Generate 2-8 paragraphs, each with at most 20 words (well under default MaxTokens=512)
        var paragraphCount = (paragraphCountSeed.Get % 7) + 2; // 2-8 paragraphs
        var maxWordsPerParagraph = (wordsSeed.Get % 15) + 3; // 3-17 words per paragraph
        var paragraphs = GenerateParagraphs(paragraphCount, maxWordsPerParagraph, randomSeed.Get);

        // Verify precondition: each paragraph fits within MaxTokens
        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);
        foreach (var para in paragraphs)
        {
            ChunkingService.CountTokens(para).Should().BeLessThanOrEqualTo(options.MaxTokens,
                "test precondition: each paragraph must fit within MaxTokens");
        }

        // Create the document by joining paragraphs with double newlines
        var rawText = string.Join("\n\n", paragraphs);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), DefaultMetadata);

        // Chunk the document
        var chunks = Service.ChunkDocument(document, options);

        // For each original paragraph, verify it appears entirely within at least one chunk
        foreach (var paragraph in paragraphs)
        {
            var paragraphWords = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // The paragraph's words should appear as a contiguous subsequence in some chunk
            var foundInChunk = chunks.Any(chunk =>
            {
                var chunkWords = chunk.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return ContainsContiguousSubsequence(chunkWords, paragraphWords);
            });

            foundInChunk.Should().BeTrue(
                $"paragraph '{Truncate(paragraph, 50)}' (with {paragraphWords.Length} tokens) should appear entirely within a single chunk, not split across chunk boundaries");
        }
    }

    [Property(MaxTest = 100)]
    public void ParagraphBoundariesRespected_WithSmallerMaxTokens(
        PositiveInt paragraphCountSeed, PositiveInt randomSeed)
    {
        // Use a smaller MaxTokens to force multiple chunks, but still keep paragraphs small enough to fit
        var maxTokens = 30;
        var options = new ChunkingOptions(MaxTokens: maxTokens, MinTokens: 5, OverlapTokens: 5);

        // Generate 3-10 paragraphs, each with 3-10 words (under maxTokens=30)
        var paragraphCount = (paragraphCountSeed.Get % 8) + 3; // 3-10 paragraphs
        var paragraphs = GenerateParagraphs(paragraphCount, maxWordsPerParagraph: 10, seed: randomSeed.Get);

        // Ensure all paragraphs actually fit within maxTokens; if not, trim them
        paragraphs = paragraphs
            .Select(p =>
            {
                var words = p.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > maxTokens)
                {
                    return string.Join(" ", words.Take(maxTokens - 1));
                }
                return p;
            })
            .ToList();

        // Verify precondition
        foreach (var para in paragraphs)
        {
            ChunkingService.CountTokens(para).Should().BeLessThanOrEqualTo(maxTokens,
                "test precondition: each paragraph must fit within MaxTokens");
        }

        var rawText = string.Join("\n\n", paragraphs);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), DefaultMetadata);

        var chunks = Service.ChunkDocument(document, options);

        // Verify each paragraph appears entirely within at least one chunk
        foreach (var paragraph in paragraphs)
        {
            var paragraphWords = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var foundInChunk = chunks.Any(chunk =>
            {
                var chunkWords = chunk.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return ContainsContiguousSubsequence(chunkWords, paragraphWords);
            });

            foundInChunk.Should().BeTrue(
                $"paragraph '{Truncate(paragraph, 50)}' should appear entirely within a single chunk");
        }
    }

    /// <summary>
    /// Checks if the haystack array contains the needle array as a contiguous subsequence.
    /// </summary>
    private static bool ContainsContiguousSubsequence(string[] haystack, string[] needle)
    {
        if (needle.Length == 0) return true;
        if (needle.Length > haystack.Length) return false;

        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }

        return false;
    }

    /// <summary>
    /// Truncates a string for display in assertion messages.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
