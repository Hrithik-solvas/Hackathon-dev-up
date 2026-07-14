using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Chunking;

public class ChunkingServiceDocumentTests
{
    private readonly ChunkingService _service;
    private readonly SourceFileMetadata _testMetadata;

    public ChunkingServiceDocumentTests()
    {
        var logger = NullLogger<ChunkingService>.Instance;
        _service = new ChunkingService(logger);
        _testMetadata = new SourceFileMetadata(
            FilePath: "/repo/docs/test.md",
            FileName: "test.md",
            FileExtension: ".md",
            LastModified: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ChunkDocument_EmptyText_ReturnsNoChunks()
    {
        var doc = new ParsedDocument("", new List<Heading>(), _testMetadata);

        var result = _service.ChunkDocument(doc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkDocument_WhitespaceOnlyText_ReturnsNoChunks()
    {
        var doc = new ParsedDocument("   \n\n   ", new List<Heading>(), _testMetadata);

        var result = _service.ChunkDocument(doc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkDocument_SingleParagraphWithinMax_ReturnsSingleChunk()
    {
        var text = "This is a simple paragraph with some words in it.";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);

        var result = _service.ChunkDocument(doc);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0);
        result[0].Metadata.SourceFilePath.Should().Be("/repo/docs/test.md");
        result[0].Metadata.ChunkIndex.Should().Be(0);
        result[0].Metadata.ContentType.Should().Be("document");
    }

    [Fact]
    public void ChunkDocument_MultipleParagraphsWithinMax_ReturnsSingleChunk()
    {
        var text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);

        var result = _service.ChunkDocument(doc, options);

        // All paragraphs together are well under 512 tokens, so should be 1 chunk
        result.Should().HaveCount(1);
        result[0].Text.Should().Contain("First paragraph");
        result[0].Text.Should().Contain("Second paragraph");
        result[0].Text.Should().Contain("Third paragraph");
    }

    [Fact]
    public void ChunkDocument_ParagraphsExceedingMax_SplitsIntoMultipleChunks()
    {
        // Create paragraphs that will exceed max tokens
        var paragraph1 = string.Join(" ", Enumerable.Range(1, 300).Select(i => $"word{i}"));
        var paragraph2 = string.Join(" ", Enumerable.Range(301, 300).Select(i => $"word{i}"));
        var text = $"{paragraph1}\n\n{paragraph2}";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 400, MinTokens: 50, OverlapTokens: 50);

        var result = _service.ChunkDocument(doc, options);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkDocument_SequentialZeroBasedIndices()
    {
        // Create enough content to generate multiple chunks
        var paragraphs = Enumerable.Range(1, 10)
            .Select(i => string.Join(" ", Enumerable.Range(1, 100).Select(j => $"word{i}_{j}")));
        var text = string.Join("\n\n", paragraphs);
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 200, MinTokens: 20, OverlapTokens: 30);

        var result = _service.ChunkDocument(doc, options);

        result.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
            result[i].Metadata.ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkDocument_AppliesOverlap_AdjacentChunksShareTokens()
    {
        // Create paragraphs with known content
        var paragraph1 = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"alpha{i}"));
        var paragraph2 = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"beta{i}"));
        var text = $"{paragraph1}\n\n{paragraph2}";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 250, MinTokens: 20, OverlapTokens: 50);

        var result = _service.ChunkDocument(doc, options);

        result.Should().HaveCountGreaterThan(1);
        // The second chunk should start with tokens from the end of the first chunk
        var firstTokens = result[0].Text.Split(' ');
        var secondTokens = result[1].Text.Split(' ');
        var overlapFromFirst = firstTokens.TakeLast(50).ToArray();
        var startOfSecond = secondTokens.Take(50).ToArray();
        overlapFromFirst.Should().BeEquivalentTo(startOfSecond);
    }

    [Fact]
    public void ChunkDocument_DefaultOptions_UsesCorrectDefaults()
    {
        var text = "A paragraph.";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);

        // Should use defaults: MaxTokens=512, MinTokens=50, OverlapTokens=50
        var result = _service.ChunkDocument(doc);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ChunkDocument_ReferencesSourceFilePath()
    {
        var text = "Some content for the document.";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);

        var result = _service.ChunkDocument(doc);

        result.Should().HaveCount(1);
        result[0].Metadata.SourceFilePath.Should().Be("/repo/docs/test.md");
    }

    [Fact]
    public void ChunkDocument_SmallTrailingContent_MergedWithPrevious()
    {
        // Create scenario: large paragraph + tiny paragraph below min threshold
        var largeParagraph = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));
        var tinyParagraph = "tiny end"; // only 2 tokens, well below min of 50
        var text = $"{largeParagraph}\n\n{tinyParagraph}";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);

        var result = _service.ChunkDocument(doc, options);

        // The tiny trailing content should be merged into the first chunk
        result.Should().HaveCount(1);
        result[0].Text.Should().Contain("tiny end");
    }

    [Fact]
    public void ChunkDocument_NoChunkExceedsMaxTokens_WhenParagraphsFit()
    {
        // Create many small paragraphs
        var paragraphs = Enumerable.Range(1, 20)
            .Select(i => string.Join(" ", Enumerable.Range(1, 40).Select(j => $"w{i}_{j}")));
        var text = string.Join("\n\n", paragraphs);
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 100, MinTokens: 10, OverlapTokens: 20);

        var result = _service.ChunkDocument(doc, options);

        foreach (var chunk in result)
        {
            var tokenCount = ChunkingService.CountTokens(chunk.Text);
            tokenCount.Should().BeLessThanOrEqualTo(options.MaxTokens + options.OverlapTokens,
                $"chunk {chunk.Index} should not significantly exceed max tokens (allowing overlap overhead)");
        }
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespace()
    {
        var result = ChunkingService.Tokenize("hello world  foo\tbar\nnewline");

        result.Should().BeEquivalentTo(new[] { "hello", "world", "foo", "bar", "newline" });
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var result = ChunkingService.Tokenize("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void CountTokens_ReturnsWordCount()
    {
        var count = ChunkingService.CountTokens("one two three four five");

        count.Should().Be(5);
    }

    [Fact]
    public void ChunkDocument_OversizedParagraph_SplitsAtSentenceBoundaries()
    {
        // Create a paragraph with many sentences that together exceed max tokens
        var sentences = Enumerable.Range(1, 20)
            .Select(i => $"This is sentence number {i} with some additional words to make it longer for testing purposes.")
            .ToList();
        var oversizedParagraph = string.Join(" ", sentences);
        var doc = new ParsedDocument(oversizedParagraph, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        // Should produce multiple chunks from the oversized paragraph
        result.Should().HaveCountGreaterThan(1);
        // Each chunk should respect the max token limit (allowing some tolerance for sentence grouping)
        foreach (var chunk in result)
        {
            ChunkingService.CountTokens(chunk.Text).Should().BeLessThanOrEqualTo(options.MaxTokens + options.OverlapTokens);
        }
    }

    [Fact]
    public void ChunkDocument_OversizedParagraph_HasContextHeader_WhenHeadingExists()
    {
        // Create document with a heading followed by an oversized paragraph
        var heading = "# Important Section";
        var oversizedContent = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}."));
        var text = $"{heading}\n\n{oversizedContent}";
        var headings = new List<Heading> { new(1, "Important Section") };
        var doc = new ParsedDocument(text, headings, _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        // The sub-chunks from the oversized paragraph should have the context header set
        var subChunks = result.Where(c => c.ContextHeader != null).ToList();
        subChunks.Should().NotBeEmpty();
        subChunks.Should().AllSatisfy(c => c.ContextHeader.Should().Be("Important Section"));
    }

    [Fact]
    public void ChunkDocument_OversizedParagraph_NullContextHeader_WhenNoHeading()
    {
        // Oversized paragraph with no headings in the document
        var oversizedContent = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}."));
        var doc = new ParsedDocument(oversizedContent, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        result.Should().HaveCountGreaterThan(1);
        result.Should().AllSatisfy(c => c.ContextHeader.Should().BeNull());
    }

    [Fact]
    public void ChunkDocument_OversizedParagraph_SequentialIndices()
    {
        var oversizedContent = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}."));
        var doc = new ParsedDocument(oversizedContent, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        result.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
            result[i].Metadata.ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkDocument_MixedSizedParagraphs_OversizedHandledCorrectly()
    {
        // A normal paragraph, then an oversized paragraph, then another normal one
        var normalPara1 = "This is a normal short paragraph.";
        var oversizedPara = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}."));
        var normalPara2 = "This is another normal paragraph.";
        var text = $"{normalPara1}\n\n{oversizedPara}\n\n{normalPara2}";
        var doc = new ParsedDocument(text, new List<Heading>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 5, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        // Should have multiple chunks total
        result.Should().HaveCountGreaterThan(2);
        // Indices should be sequential
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void SplitAtSentenceBoundaries_SplitsCorrectly()
    {
        var text = "First sentence. Second sentence! Third sentence? Fourth sentence.";

        var result = ChunkingService.SplitAtSentenceBoundaries(text);

        result.Should().HaveCount(4);
        result[0].Should().Be("First sentence.");
        result[1].Should().Be("Second sentence!");
        result[2].Should().Be("Third sentence?");
        result[3].Should().Be("Fourth sentence.");
    }

    [Fact]
    public void SplitAtSentenceBoundaries_NoSentenceEndings_ReturnsSingleElement()
    {
        var text = "This has no sentence ending punctuation followed by space";

        var result = ChunkingService.SplitAtSentenceBoundaries(text);

        result.Should().HaveCount(1);
        result[0].Should().Be(text);
    }

    [Fact]
    public void ChunkDocument_OversizedParagraph_PicksNearestHeading()
    {
        // Document with multiple headings; the oversized paragraph is under the second heading
        var text = "# First Heading\n\nSmall paragraph under first heading.\n\n# Second Heading\n\n" +
                   string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}."));
        var headings = new List<Heading>
        {
            new(1, "First Heading"),
            new(1, "Second Heading")
        };
        var doc = new ParsedDocument(text, headings, _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 5, OverlapTokens: 10);

        var result = _service.ChunkDocument(doc, options);

        // Sub-chunks from the oversized paragraph should reference "Second Heading"
        var subChunksWithHeader = result.Where(c => c.ContextHeader != null).ToList();
        subChunksWithHeader.Should().NotBeEmpty();
        subChunksWithHeader.Should().AllSatisfy(c => c.ContextHeader.Should().Be("Second Heading"));
    }
}
