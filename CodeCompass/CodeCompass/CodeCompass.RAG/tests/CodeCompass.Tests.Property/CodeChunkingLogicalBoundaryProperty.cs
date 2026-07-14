using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 7: Code chunking respects logical boundaries.
/// For any parsed code where individual logical units fit within the maximum chunk size,
/// no chunk boundary shall fall within a logical unit—each logical unit appears entirely within a single chunk.
///
/// **Validates: Requirements 3.2**
/// </summary>
public class CodeChunkingLogicalBoundaryProperty
{
    private static readonly ChunkingService Service =
        new(NullLogger<ChunkingService>.Instance);

    private static readonly SourceFileMetadata DefaultMetadata =
        new(
            FilePath: "/test/code.cs",
            FileName: "code.cs",
            FileExtension: ".cs",
            LastModified: DateTimeOffset.UtcNow);

    /// <summary>
    /// Generates simple C# method bodies that each fit within maxTokens.
    /// Each method is a distinct logical unit with a known symbol.
    /// </summary>
    private static (string rawText, List<CodeSymbol> symbols, List<string> methodBodies) GenerateCodeWithMethods(
        int methodCount, int maxWordsPerMethod, int seed)
    {
        var rng = new Random(seed);
        var symbols = new List<CodeSymbol>();
        var methodBodies = new List<string>();
        var lines = new List<string>();

        for (int i = 0; i < methodCount; i++)
        {
            var methodName = $"Method{i}";
            symbols.Add(new CodeSymbol(methodName, CodeSymbolKind.Method, "TestClass"));

            // Generate a method with a few statements
            var statementsCount = rng.Next(1, Math.Max(2, maxWordsPerMethod / 5));
            var bodyLines = new List<string>();
            bodyLines.Add($"public void {methodName}()");
            bodyLines.Add("{");
            for (int s = 0; s < statementsCount; s++)
            {
                bodyLines.Add($"    var x{s} = {rng.Next(1, 100)};");
            }
            bodyLines.Add("}");

            var methodText = string.Join("\n", bodyLines);
            methodBodies.Add(methodText);
            lines.Add(methodText);
        }

        var rawText = string.Join("\n\n", lines);
        return (rawText, symbols, methodBodies);
    }

    [Property(MaxTest = 100)]
    public void EachLogicalUnitAppearsEntirelyWithinASingleChunk_WhenUnitsFitInMaxTokens(
        PositiveInt methodCountSeed, PositiveInt wordsSeed, PositiveInt randomSeed)
    {
        // Generate 2-6 methods, each small enough to fit within MaxTokens
        var methodCount = (methodCountSeed.Get % 5) + 2; // 2-6 methods
        var maxWordsPerMethod = (wordsSeed.Get % 10) + 5; // 5-14 words per method
        var (rawText, symbols, methodBodies) = GenerateCodeWithMethods(methodCount, maxWordsPerMethod, randomSeed.Get);

        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);

        // Verify precondition: each logical unit fits within MaxTokens
        foreach (var body in methodBodies)
        {
            ChunkingService.CountTokens(body).Should().BeLessThanOrEqualTo(options.MaxTokens,
                "test precondition: each logical unit must fit within MaxTokens");
        }

        var parsedCode = new ParsedCode(rawText, symbols, Array.Empty<string>(), DefaultMetadata);

        // Chunk the code
        var chunks = Service.ChunkCode(parsedCode, options);

        // For each logical unit, verify it appears entirely within at least one chunk
        foreach (var body in methodBodies)
        {
            var unitWords = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var foundInChunk = chunks.Any(chunk =>
            {
                var chunkWords = chunk.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return ContainsContiguousSubsequence(chunkWords, unitWords);
            });

            foundInChunk.Should().BeTrue(
                $"logical unit '{Truncate(body, 60)}' (with {unitWords.Length} tokens) should appear entirely within a single chunk");
        }
    }

    [Property(MaxTest = 100)]
    public void LogicalBoundariesRespected_WithSmallerMaxTokens(
        PositiveInt methodCountSeed, PositiveInt randomSeed)
    {
        // Use a smaller MaxTokens to force multiple chunks, but still keep methods small enough to fit
        var maxTokens = 40;
        var options = new ChunkingOptions(MaxTokens: maxTokens, MinTokens: 5, OverlapTokens: 5);

        // Generate 3-8 methods, each with very few statements
        var methodCount = (methodCountSeed.Get % 6) + 3;
        var (rawText, symbols, methodBodies) = GenerateCodeWithMethods(methodCount, maxWordsPerMethod: 5, seed: randomSeed.Get);

        // Ensure all methods actually fit within maxTokens; if not, trim them
        var validBodies = methodBodies
            .Where(b => ChunkingService.CountTokens(b) <= maxTokens)
            .ToList();

        if (validBodies.Count < 2)
            return; // Skip if we can't generate valid test data

        var parsedCode = new ParsedCode(rawText, symbols, Array.Empty<string>(), DefaultMetadata);
        var chunks = Service.ChunkCode(parsedCode, options);

        // Verify each valid logical unit appears entirely within at least one chunk
        foreach (var body in validBodies)
        {
            var unitWords = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var foundInChunk = chunks.Any(chunk =>
            {
                var chunkWords = chunk.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return ContainsContiguousSubsequence(chunkWords, unitWords);
            });

            foundInChunk.Should().BeTrue(
                $"logical unit '{Truncate(body, 60)}' should appear entirely within a single chunk");
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
