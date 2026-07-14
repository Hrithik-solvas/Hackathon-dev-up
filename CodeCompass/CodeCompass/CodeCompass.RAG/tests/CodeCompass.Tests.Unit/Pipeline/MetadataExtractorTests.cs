using CodeCompass.Core.Models;
using CodeCompass.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Pipeline;

public class MetadataExtractorTests
{
    private readonly MetadataExtractor _extractor;
    private readonly DateTimeOffset _testTimestamp = new(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);

    public MetadataExtractorTests()
    {
        var logger = NullLogger<MetadataExtractor>.Instance;
        _extractor = new MetadataExtractor(logger);
    }

    #region Task 5.1: ExtractDocumentMetadata

    [Fact]
    public void ExtractDocumentMetadata_PopulatesSourceFilePath()
    {
        var metadata = CreateDocumentMetadata("/repo/docs/guide.md", ".md");
        var document = new ParsedDocument("Some text", new List<Heading>(), metadata);

        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        result.SourceFilePath.Should().Be("/repo/docs/guide.md");
    }

    [Fact]
    public void ExtractDocumentMetadata_PopulatesChunkIndex()
    {
        var document = CreateSimpleDocument();

        var result = _extractor.ExtractDocumentMetadata(document, 5, null);

        result.ChunkIndex.Should().Be(5);
    }

    [Fact]
    public void ExtractDocumentMetadata_SetsContentTypeToDocument()
    {
        var document = CreateSimpleDocument();

        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        result.ContentType.Should().Be("document");
    }

    [Fact]
    public void ExtractDocumentMetadata_SetsLanguageToNull()
    {
        var document = CreateSimpleDocument();

        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        result.Language.Should().BeNull();
    }

    [Fact]
    public void ExtractDocumentMetadata_PopulatesLastModified()
    {
        var metadata = new SourceFileMetadata("/repo/test.md", "test.md", ".md", _testTimestamp);
        var document = new ParsedDocument("Text", new List<Heading>(), metadata);

        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        result.LastModified.Should().Be(_testTimestamp);
    }

    [Fact]
    public void ExtractDocumentMetadata_PopulatesSectionHeading()
    {
        var document = CreateSimpleDocument();

        var result = _extractor.ExtractDocumentMetadata(document, 0, "Introduction > Getting Started");

        result.SectionHeading.Should().Be("Introduction > Getting Started");
    }

    [Fact]
    public void ExtractDocumentMetadata_NullHeading_SetsSectionHeadingToNull()
    {
        var document = CreateSimpleDocument();

        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        result.SectionHeading.Should().BeNull();
    }

    #endregion

    #region Task 5.1: ExtractCodeMetadata

    [Fact]
    public void ExtractCodeMetadata_PopulatesSourceFilePath()
    {
        var code = CreateSimpleCode("/repo/src/Service.cs", ".cs");

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.SourceFilePath.Should().Be("/repo/src/Service.cs");
    }

    [Fact]
    public void ExtractCodeMetadata_PopulatesChunkIndex()
    {
        var code = CreateSimpleCode("/repo/src/App.tsx", ".tsx");

        var result = _extractor.ExtractCodeMetadata(code, 3, null);

        result.ChunkIndex.Should().Be(3);
    }

    [Fact]
    public void ExtractCodeMetadata_SetsContentTypeToCode()
    {
        var code = CreateSimpleCode("/repo/src/Test.cs", ".cs");

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.ContentType.Should().Be("code");
    }

    [Theory]
    [InlineData(".cs", "csharp")]
    [InlineData(".jsx", "javascript")]
    [InlineData(".tsx", "typescript")]
    [InlineData(".js", "javascript")]
    [InlineData(".ts", "typescript")]
    [InlineData(".sql", "sql")]
    public void ExtractCodeMetadata_MapsFileExtensionToLanguage(string extension, string expectedLanguage)
    {
        var code = CreateSimpleCode($"/repo/src/file{extension}", extension);

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.Language.Should().Be(expectedLanguage);
    }

    [Theory]
    [InlineData(".md")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".txt")]
    [InlineData(".py")]
    public void ExtractCodeMetadata_UnknownExtension_ReturnsNullLanguage(string extension)
    {
        var code = CreateSimpleCode($"/repo/src/file{extension}", extension);

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.Language.Should().BeNull();
    }

    [Fact]
    public void ExtractCodeMetadata_PopulatesLastModified()
    {
        var metadata = new SourceFileMetadata("/repo/src/Test.cs", "Test.cs", ".cs", _testTimestamp);
        var code = new ParsedCode("code", new List<CodeSymbol>(), new List<string>(), metadata);

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.LastModified.Should().Be(_testTimestamp);
    }

    [Fact]
    public void ExtractCodeMetadata_PopulatesContainingSymbolAsSectionHeading()
    {
        var code = CreateSimpleCode("/repo/src/Test.cs", ".cs");

        var result = _extractor.ExtractCodeMetadata(code, 0, "MyClass.MyMethod");

        result.SectionHeading.Should().Be("MyClass.MyMethod");
    }

    [Fact]
    public void ExtractCodeMetadata_NullSymbol_SetsSectionHeadingToNull()
    {
        var code = CreateSimpleCode("/repo/src/Test.cs", ".cs");

        var result = _extractor.ExtractCodeMetadata(code, 0, null);

        result.SectionHeading.Should().BeNull();
    }

    #endregion

    #region Task 5.2: ResolveNearestAncestorHeading

    [Fact]
    public void ResolveNearestAncestorHeading_EmptyHeadings_ReturnsNull()
    {
        var headings = new List<Heading>();
        var text = "Some document text without headings.";

        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, 10);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveNearestAncestorHeading_EmptyText_ReturnsNull()
    {
        var headings = new List<Heading> { new(1, "Introduction") };

        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, "", 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveNearestAncestorHeading_NegativePosition_ReturnsNull()
    {
        var headings = new List<Heading> { new(1, "Introduction") };
        var text = "Introduction\nSome content here.";

        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, -1);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveNearestAncestorHeading_PositionBeforeFirstHeading_ReturnsNull()
    {
        var text = "Preamble text.\n\nIntroduction\nContent after heading.";
        var headings = new List<Heading> { new(1, "Introduction") };

        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, 5);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveNearestAncestorHeading_SingleHeading_ReturnsHeadingText()
    {
        var text = "Introduction\n\nThis is the content under the introduction.";
        var headings = new List<Heading> { new(1, "Introduction") };

        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, 20);

        result.Should().Be("Introduction");
    }

    [Fact]
    public void ResolveNearestAncestorHeading_MultipleHeadingsSameLevel_ReturnsNearestPreceding()
    {
        var text = "Chapter 1\n\nContent of chapter 1.\n\nChapter 2\n\nContent of chapter 2.";
        var headings = new List<Heading>
        {
            new(1, "Chapter 1"),
            new(1, "Chapter 2")
        };

        // Position in "Content of chapter 2."
        var position = text.IndexOf("Content of chapter 2.", StringComparison.Ordinal);
        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, position);

        result.Should().Be("Chapter 2");
    }

    [Fact]
    public void ResolveNearestAncestorHeading_NestedHeadings_BuildsHierarchy()
    {
        var text = "Introduction\n\nSetup\n\nPrerequisites\n\nYou need to install X.";
        var headings = new List<Heading>
        {
            new(1, "Introduction"),
            new(2, "Setup"),
            new(3, "Prerequisites")
        };

        // Position in "You need to install X."
        var position = text.IndexOf("You need to install X.", StringComparison.Ordinal);
        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, position);

        result.Should().Be("Introduction > Setup > Prerequisites");
    }

    [Fact]
    public void ResolveNearestAncestorHeading_HeadingAtSameLevel_ResetsHierarchy()
    {
        var text = "Chapter 1\n\nSection A\n\nContent A.\n\nSection B\n\nContent B.";
        var headings = new List<Heading>
        {
            new(1, "Chapter 1"),
            new(2, "Section A"),
            new(2, "Section B")
        };

        // Position in "Content B."
        var position = text.IndexOf("Content B.", StringComparison.Ordinal);
        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, position);

        result.Should().Be("Chapter 1 > Section B");
    }

    [Fact]
    public void ResolveNearestAncestorHeading_DeeperHeadingFollowedByShallower_PopsStack()
    {
        var text = "Intro\n\nDetails\n\nSubdetails\n\nOverview\n\nContent here.";
        var headings = new List<Heading>
        {
            new(1, "Intro"),
            new(2, "Details"),
            new(3, "Subdetails"),
            new(2, "Overview")
        };

        // Position in "Content here." which is after "Overview" (H2)
        var position = text.IndexOf("Content here.", StringComparison.Ordinal);
        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, position);

        result.Should().Be("Intro > Overview");
    }

    [Fact]
    public void ResolveNearestAncestorHeading_H1FollowedByH3_BuildsHierarchyAcrossLevels()
    {
        var text = "Main Title\n\nDeep Section\n\nContent under deep section.";
        var headings = new List<Heading>
        {
            new(1, "Main Title"),
            new(3, "Deep Section")
        };

        var position = text.IndexOf("Content under deep section.", StringComparison.Ordinal);
        var result = MetadataExtractor.ResolveNearestAncestorHeading(headings, text, position);

        result.Should().Be("Main Title > Deep Section");
    }

    #endregion

    #region Task 5.2: ResolveHeadingsForChunks

    [Fact]
    public void ResolveHeadingsForChunks_NoHeadings_ReturnsAllNulls()
    {
        var metadata = CreateDocumentMetadata("/repo/test.md", ".md");
        var document = new ParsedDocument("Some content here.", new List<Heading>(), metadata);
        var chunks = new List<Chunk>
        {
            new("Some content", 0, null, new ChunkMetadata("/repo/test.md", 0, "document", null, _testTimestamp, null))
        };

        var result = MetadataExtractor.ResolveHeadingsForChunks(document, chunks);

        result.Should().HaveCount(1);
        result[0].Should().BeNull();
    }

    [Fact]
    public void ResolveHeadingsForChunks_EmptyChunks_ReturnsEmptyList()
    {
        var headings = new List<Heading> { new(1, "Title") };
        var metadata = CreateDocumentMetadata("/repo/test.md", ".md");
        var document = new ParsedDocument("Title\n\nContent", headings, metadata);

        var result = MetadataExtractor.ResolveHeadingsForChunks(document, new List<Chunk>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveHeadingsForChunks_SingleChunkUnderHeading_ReturnsHeading()
    {
        var text = "Introduction\n\nThis is the introduction content.";
        var headings = new List<Heading> { new(1, "Introduction") };
        var metadata = CreateDocumentMetadata("/repo/test.md", ".md");
        var document = new ParsedDocument(text, headings, metadata);
        var chunks = new List<Chunk>
        {
            new("This is the introduction content.", 0, null,
                new ChunkMetadata("/repo/test.md", 0, "document", null, _testTimestamp, null))
        };

        var result = MetadataExtractor.ResolveHeadingsForChunks(document, chunks);

        result.Should().HaveCount(1);
        result[0].Should().Be("Introduction");
    }

    [Fact]
    public void ResolveHeadingsForChunks_MultipleChunksUnderDifferentHeadings_AssignsCorrectly()
    {
        var text = "Chapter 1\n\nFirst chapter content.\n\nChapter 2\n\nSecond chapter content.";
        var headings = new List<Heading>
        {
            new(1, "Chapter 1"),
            new(1, "Chapter 2")
        };
        var metadata = CreateDocumentMetadata("/repo/test.md", ".md");
        var document = new ParsedDocument(text, headings, metadata);
        var chunks = new List<Chunk>
        {
            new("First chapter content.", 0, null,
                new ChunkMetadata("/repo/test.md", 0, "document", null, _testTimestamp, null)),
            new("Second chapter content.", 1, null,
                new ChunkMetadata("/repo/test.md", 1, "document", null, _testTimestamp, null))
        };

        var result = MetadataExtractor.ResolveHeadingsForChunks(document, chunks);

        result.Should().HaveCount(2);
        result[0].Should().Be("Chapter 1");
        result[1].Should().Be("Chapter 2");
    }

    [Fact]
    public void ResolveHeadingsForChunks_NestedHeadings_BuildsHierarchyForEachChunk()
    {
        var text = "Guide\n\nInstallation\n\nStep 1: Download.\n\nConfiguration\n\nStep 2: Configure.";
        var headings = new List<Heading>
        {
            new(1, "Guide"),
            new(2, "Installation"),
            new(2, "Configuration")
        };
        var metadata = CreateDocumentMetadata("/repo/test.md", ".md");
        var document = new ParsedDocument(text, headings, metadata);
        var chunks = new List<Chunk>
        {
            new("Step 1: Download.", 0, null,
                new ChunkMetadata("/repo/test.md", 0, "document", null, _testTimestamp, null)),
            new("Step 2: Configure.", 1, null,
                new ChunkMetadata("/repo/test.md", 1, "document", null, _testTimestamp, null))
        };

        var result = MetadataExtractor.ResolveHeadingsForChunks(document, chunks);

        result.Should().HaveCount(2);
        result[0].Should().Be("Guide > Installation");
        result[1].Should().Be("Guide > Configuration");
    }

    #endregion

    #region Helpers

    private SourceFileMetadata CreateDocumentMetadata(string filePath, string extension)
    {
        var fileName = Path.GetFileName(filePath);
        return new SourceFileMetadata(filePath, fileName, extension, _testTimestamp);
    }

    private ParsedDocument CreateSimpleDocument()
    {
        var metadata = CreateDocumentMetadata("/repo/docs/test.md", ".md");
        return new ParsedDocument("Some test content.", new List<Heading>(), metadata);
    }

    private ParsedCode CreateSimpleCode(string filePath, string extension)
    {
        var fileName = Path.GetFileName(filePath);
        var metadata = new SourceFileMetadata(filePath, fileName, extension, _testTimestamp);
        return new ParsedCode("public class Test { }", new List<CodeSymbol>(), new List<string>(), metadata);
    }

    #endregion
}
