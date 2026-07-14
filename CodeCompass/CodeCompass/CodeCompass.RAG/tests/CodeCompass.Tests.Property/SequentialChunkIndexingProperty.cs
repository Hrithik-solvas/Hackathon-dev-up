using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 10: Sequential chunk indexing is correct.
/// For any chunking output, indices form a zero-based contiguous sequence
/// and each chunk references the correct source file path.
///
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class SequentialChunkIndexingProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly string[] WordPool = new[]
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta",
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing"
    };

    private static string GenerateDocument(int paragraphCount, int wordsPerParagraph, int seed)
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

    private static (string rawText, List<CodeSymbol> symbols) GenerateCode(int methodCount, int seed)
    {
        var rng = new Random(seed);
        var symbols = new List<CodeSymbol>();
        var lines = new List<string>();

        for (int i = 0; i < methodCount; i++)
        {
            var methodName = $"Method{i}";
            symbols.Add(new CodeSymbol(methodName, CodeSymbolKind.Method, "TestClass"));
            var bodyLines = new List<string>
            {
                $"public void {methodName}()",
                "{",
                $"    var x = {rng.Next(1, 100)};",
                $"    var y = {rng.Next(1, 100)};",
                "}"
            };
            lines.Add(string.Join("\n", bodyLines));
        }

        return (string.Join("\n\n", lines), symbols);
    }

    [Property(MaxTest = 100)]
    public void DocumentChunks_HaveZeroBasedContiguousIndices(PositiveInt paragraphSeed, PositiveInt randomSeed)
    {
        var filePath = $"/test/doc_{randomSeed.Get}.md";
        var metadata = new SourceFileMetadata(
            FilePath: filePath,
            FileName: $"doc_{randomSeed.Get}.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);

        var paragraphCount = (paragraphSeed.Get % 8) + 3;
        var options = new ChunkingOptions(MaxTokens: 60, MinTokens: 5, OverlapTokens: 10);
        var rawText = GenerateDocument(paragraphCount, wordsPerParagraph: 15, seed: randomSeed.Get);
        var document = new ParsedDocument(rawText, Array.Empty<Heading>(), metadata);

        var chunks = Service.ChunkDocument(document, options);

        if (chunks.Count == 0) return;

        // Verify indices form 0, 1, 2, ... sequence
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i,
                $"chunk at position {i} should have Index={i}");
        }

        // Verify each chunk's Metadata.SourceFilePath matches the input
        foreach (var chunk in chunks)
        {
            chunk.Metadata.SourceFilePath.Should().Be(filePath,
                $"chunk {chunk.Index} SourceFilePath should match input metadata");
        }

        // Verify each chunk's Metadata.ChunkIndex matches chunk.Index
        foreach (var chunk in chunks)
        {
            chunk.Metadata.ChunkIndex.Should().Be(chunk.Index,
                $"chunk {chunk.Index} Metadata.ChunkIndex should match chunk.Index");
        }
    }

    [Property(MaxTest = 100)]
    public void CodeChunks_HaveZeroBasedContiguousIndices(PositiveInt methodSeed, PositiveInt randomSeed)
    {
        var filePath = $"/test/code_{randomSeed.Get}.cs";
        var metadata = new SourceFileMetadata(
            FilePath: filePath,
            FileName: $"code_{randomSeed.Get}.cs",
            FileExtension: ".cs",
            LastModified: DateTimeOffset.UtcNow);

        var methodCount = (methodSeed.Get % 6) + 3;
        var options = new ChunkingOptions(MaxTokens: 60, MinTokens: 5, OverlapTokens: 10);
        var (rawText, symbols) = GenerateCode(methodCount, seed: randomSeed.Get);
        var parsedCode = new ParsedCode(rawText, symbols, Array.Empty<string>(), metadata);

        var chunks = Service.ChunkCode(parsedCode, options);

        if (chunks.Count == 0) return;

        // Verify indices form 0, 1, 2, ... sequence
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i,
                $"code chunk at position {i} should have Index={i}");
        }

        // Verify each chunk's Metadata.SourceFilePath matches the input
        foreach (var chunk in chunks)
        {
            chunk.Metadata.SourceFilePath.Should().Be(filePath,
                $"code chunk {chunk.Index} SourceFilePath should match input metadata");
        }

        // Verify each chunk's Metadata.ChunkIndex matches chunk.Index
        foreach (var chunk in chunks)
        {
            chunk.Metadata.ChunkIndex.Should().Be(chunk.Index,
                $"code chunk {chunk.Index} Metadata.ChunkIndex should match chunk.Index");
        }
    }
}
