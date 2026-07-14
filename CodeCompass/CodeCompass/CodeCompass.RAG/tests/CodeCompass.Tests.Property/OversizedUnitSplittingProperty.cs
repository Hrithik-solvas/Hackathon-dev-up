using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 11: Oversized unit splitting.
/// For any logical unit exceeding max chunk size, splitting occurs at sentence/statement
/// boundaries and each sub-chunk is prepended with a context header.
///
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class OversizedUnitSplittingProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly string[] WordPool = new[]
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta",
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
        "code", "compass", "pipeline", "document", "chunking", "parsing"
    };

    /// <summary>
    /// Generates an oversized paragraph (exceeds maxTokens) made of sentences.
    /// </summary>
    private static string GenerateOversizedParagraph(int maxTokens, int seed)
    {
        var rng = new Random(seed);
        var sentences = new List<string>();
        var totalTokens = 0;

        // Generate sentences until we exceed maxTokens by a good margin
        while (totalTokens < maxTokens * 2)
        {
            var wordCount = rng.Next(5, 12);
            var words = new List<string>();
            for (int w = 0; w < wordCount; w++)
            {
                words.Add(WordPool[rng.Next(WordPool.Length)]);
            }
            var sentence = string.Join(" ", words) + ".";
            sentences.Add(sentence);
            totalTokens += TokenCounter.CountTokens(sentence);
        }

        return string.Join(" ", sentences);
    }

    /// <summary>
    /// Generates an oversized code unit (exceeds maxTokens) made of statements.
    /// </summary>
    private static string GenerateOversizedCodeUnit(int maxTokens, int seed)
    {
        var rng = new Random(seed);
        var lines = new List<string>();
        lines.Add("public void OversizedMethod()");
        lines.Add("{");

        var totalTokens = TokenCounter.CountTokens(string.Join("\n", lines));

        // Generate statements until we exceed maxTokens by a good margin
        int varIndex = 0;
        while (totalTokens < maxTokens * 2)
        {
            var stmt = $"    var variable{varIndex} = {rng.Next(1, 10000)};";
            lines.Add(stmt);
            totalTokens += TokenCounter.CountTokens(stmt);
            varIndex++;
        }

        lines.Add("}");
        return string.Join("\n", lines);
    }

    [Property(MaxTest = 50)]
    public void OversizedDocumentParagraph_ProducesChunksWithContextHeader(
        PositiveInt randomSeed)
    {
        var maxTokens = 30;
        var options = new ChunkingOptions(MaxTokens: maxTokens, MinTokens: 5, OverlapTokens: 5);
        var headingText = "Important Section";

        // Create a document with a heading followed by an oversized paragraph
        var oversizedParagraph = GenerateOversizedParagraph(maxTokens, randomSeed.Get);
        var rawText = $"# {headingText}\n\n{oversizedParagraph}";

        var headings = new List<Heading> { new Heading(1, headingText) };
        var metadata = new SourceFileMetadata(
            FilePath: "/test/oversized.md",
            FileName: "oversized.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);

        var document = new ParsedDocument(rawText, headings, metadata);

        // Verify precondition: the paragraph exceeds MaxTokens
        TokenCounter.CountTokens(oversizedParagraph).Should().BeGreaterThan(maxTokens,
            "test precondition: paragraph must exceed MaxTokens");

        var chunks = Service.ChunkDocument(document, options);

        // Find chunks that come from the oversized splitting (have ContextHeader set)
        var oversizedChunks = chunks.Where(c => c.ContextHeader != null).ToList();

        // There should be at least 2 chunks from the oversized paragraph
        oversizedChunks.Count.Should().BeGreaterThanOrEqualTo(2,
            "oversized paragraph should be split into 2+ sub-chunks");

        // Each sub-chunk from the oversized paragraph should have ContextHeader set
        foreach (var chunk in oversizedChunks)
        {
            chunk.ContextHeader.Should().NotBeNullOrEmpty(
                $"chunk {chunk.Index} from oversized splitting should have a non-null context header");
        }

        // For documents, context header should be the heading text
        foreach (var chunk in oversizedChunks)
        {
            chunk.ContextHeader.Should().Be(headingText,
                $"chunk {chunk.Index} context header should be the nearest ancestor heading");
        }
    }

    [Property(MaxTest = 50)]
    public void OversizedCodeUnit_ProducesChunksWithDeclarationSignatureHeader(
        PositiveInt randomSeed)
    {
        var maxTokens = 30;
        var options = new ChunkingOptions(MaxTokens: maxTokens, MinTokens: 5, OverlapTokens: 5);

        var oversizedUnit = GenerateOversizedCodeUnit(maxTokens, randomSeed.Get);
        var expectedSignature = "public void OversizedMethod()";

        var symbols = new List<CodeSymbol>
        {
            new CodeSymbol("OversizedMethod", CodeSymbolKind.Method, "TestClass")
        };

        var metadata = new SourceFileMetadata(
            FilePath: "/test/oversized.cs",
            FileName: "oversized.cs",
            FileExtension: ".cs",
            LastModified: DateTimeOffset.UtcNow);

        var parsedCode = new ParsedCode(oversizedUnit, symbols, Array.Empty<string>(), metadata);

        // Verify precondition: the code unit exceeds MaxTokens
        TokenCounter.CountTokens(oversizedUnit).Should().BeGreaterThan(maxTokens,
            "test precondition: code unit must exceed MaxTokens");

        var chunks = Service.ChunkCode(parsedCode, options);

        // Find chunks that come from the oversized splitting (have ContextHeader set)
        var oversizedChunks = chunks.Where(c => c.ContextHeader != null).ToList();

        // There should be at least 2 chunks from the oversized code unit
        oversizedChunks.Count.Should().BeGreaterThanOrEqualTo(2,
            "oversized code unit should be split into 2+ sub-chunks");

        // Each sub-chunk should have ContextHeader set to the declaration signature
        foreach (var chunk in oversizedChunks)
        {
            chunk.ContextHeader.Should().NotBeNullOrEmpty(
                $"code chunk {chunk.Index} from oversized splitting should have a context header");
            chunk.ContextHeader.Should().Be(expectedSignature,
                $"code chunk {chunk.Index} context header should be the declaration signature (first line)");
        }
    }
}
