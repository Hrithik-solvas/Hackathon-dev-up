using CodeCompass.Core.Models;
using CodeCompass.Pipeline;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 19: Metadata extraction completeness.
/// For any parsed content (document or code), the metadata extractor produces a ChunkMetadata
/// with all VectorStore schema fields populated, with correct heading hierarchy for documents
/// and language/namespace/class for code.
///
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
/// </summary>
public class MetadataExtractionCompletenessProperty
{
    private readonly MetadataExtractor _extractor;

    private static readonly string[] SupportedCodeExtensions = { ".cs", ".jsx", ".tsx", ".js", ".ts", ".sql" };
    private static readonly string[] ExpectedLanguages = { "csharp", "javascript", "typescript", "javascript", "typescript", "sql" };

    public MetadataExtractionCompletenessProperty()
    {
        var logger = NullLogger<MetadataExtractor>.Instance;
        _extractor = new MetadataExtractor(logger);
    }

    #region Document Metadata Completeness

    [Property(MaxTest = 100)]
    public void DocumentMetadata_AllFieldsPopulated_ForParsedDocumentWithHeadings(
        PositiveInt chunkIndexSeed,
        PositiveInt headingCountSeed)
    {
        // Arrange
        var chunkIndex = chunkIndexSeed.Get % 20; // 0-19
        var headingCount = (headingCountSeed.Get % 5) + 1; // 1-5 headings

        var headings = GenerateHeadings(headingCount);
        var rawText = GenerateDocumentTextWithHeadings(headings);
        var filePath = $"/repo/docs/guide_{chunkIndexSeed.Get}.md";
        var lastModified = DateTimeOffset.UtcNow.AddDays(-(chunkIndexSeed.Get % 365));
        var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), ".md", lastModified);
        var document = new ParsedDocument(rawText, headings, metadata);

        // Determine the heading for this chunk by computing a reasonable position
        var chunkStartPosition = rawText.Length / (chunkIndex + 1);
        var nearestHeading = MetadataExtractor.ResolveNearestAncestorHeading(
            headings, rawText, chunkStartPosition);

        // Act
        var result = _extractor.ExtractDocumentMetadata(document, chunkIndex, nearestHeading);

        // Assert: All VectorStore schema fields are populated
        result.SourceFilePath.Should().NotBeNullOrWhiteSpace("SourceFilePath must be populated");
        result.SourceFilePath.Should().Be(filePath);
        result.ChunkIndex.Should().Be(chunkIndex, "ChunkIndex must match the provided chunk index");
        result.ContentType.Should().Be("document", "ContentType must be 'document' for document files");
        result.LastModified.Should().NotBe(default(DateTimeOffset), "LastModified must not be default");
        result.LastModified.Should().Be(lastModified);

        // For documents with headings, SectionHeading reflects heading hierarchy
        if (chunkStartPosition > 0 && headings.Count > 0)
        {
            // At least some chunks should have headings assigned
            // (the first chunk might not if it precedes all headings)
            result.SectionHeading.Should().NotBeNullOrWhiteSpace(
                "SectionHeading should reflect heading hierarchy when headings precede chunk position");
        }
    }

    [Property(MaxTest = 100)]
    public void DocumentMetadata_HeadingHierarchyIsCorrect_ForNestedHeadings(
        PositiveInt depthSeed)
    {
        // Arrange: create a document with nested headings (H1 > H2 > H3)
        var depth = (depthSeed.Get % 3) + 2; // 2-4 levels deep
        var headings = new List<Heading>();
        var textParts = new List<string>();

        for (int i = 1; i <= depth; i++)
        {
            var headingText = $"Level{i}Heading";
            headings.Add(new Heading(i, headingText));
            textParts.Add(headingText);
            textParts.Add($"Content under level {i}.");
        }
        textParts.Add("Final content after all headings.");

        var rawText = string.Join("\n\n", textParts);
        var filePath = "/repo/docs/nested.md";
        var lastModified = DateTimeOffset.UtcNow;
        var metadata = new SourceFileMetadata(filePath, "nested.md", ".md", lastModified);
        var document = new ParsedDocument(rawText, headings, metadata);

        // Position chunk at the end of the document (after all headings)
        var chunkPosition = rawText.Length - 10;
        var nearestHeading = MetadataExtractor.ResolveNearestAncestorHeading(
            headings, rawText, chunkPosition);

        // Act
        var result = _extractor.ExtractDocumentMetadata(document, 0, nearestHeading);

        // Assert: heading hierarchy should contain all levels joined with " > "
        result.SectionHeading.Should().NotBeNullOrWhiteSpace();
        var headingParts = result.SectionHeading!.Split(" > ");
        headingParts.Length.Should().Be(depth, "hierarchy depth should match the number of heading levels");

        for (int i = 0; i < depth; i++)
        {
            headingParts[i].Should().Be($"Level{i + 1}Heading");
        }
    }

    [Property(MaxTest = 100)]
    public void DocumentMetadata_SourceFilePathAndTimestamp_AlwaysPopulated(
        PositiveInt pathSeed, PositiveInt daysSeed)
    {
        // Arrange: any document with valid metadata
        var filePath = $"/repo/docs/file_{pathSeed.Get % 100}.md";
        var daysAgo = daysSeed.Get % 1000;
        var lastModified = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        var metadata = new SourceFileMetadata(filePath, Path.GetFileName(filePath), ".md", lastModified);
        var document = new ParsedDocument("Some content.", new List<Heading>(), metadata);

        // Act
        var result = _extractor.ExtractDocumentMetadata(document, 0, null);

        // Assert
        result.SourceFilePath.Should().NotBeNullOrWhiteSpace();
        result.LastModified.Should().NotBe(default(DateTimeOffset));
        result.ContentType.Should().Be("document");
        result.ChunkIndex.Should().Be(0);
    }

    #endregion

    #region Code Metadata Completeness

    [Property(MaxTest = 100)]
    public void CodeMetadata_AllFieldsPopulated_LanguageCorrectlyMapped(
        PositiveInt extensionSeed, PositiveInt chunkIndexSeed)
    {
        // Arrange
        var extIndex = extensionSeed.Get % SupportedCodeExtensions.Length;
        var extension = SupportedCodeExtensions[extIndex];
        var expectedLanguage = ExpectedLanguages[extIndex];
        var chunkIndex = chunkIndexSeed.Get % 10;

        var filePath = $"/repo/src/MyService{extension}";
        var lastModified = DateTimeOffset.UtcNow.AddHours(-(chunkIndexSeed.Get % 1000));
        var metadata = new SourceFileMetadata(filePath, $"MyService{extension}", extension, lastModified);

        var symbols = new List<CodeSymbol>
        {
            new("MyNamespace", CodeSymbolKind.Class, null),
            new("MyClass", CodeSymbolKind.Class, "MyNamespace"),
            new("MyMethod", CodeSymbolKind.Method, "MyClass")
        };

        var code = new ParsedCode("public class MyClass { void MyMethod() {} }", symbols, new List<string>(), metadata);

        // Act
        var result = _extractor.ExtractCodeMetadata(code, chunkIndex, null);

        // Assert: All VectorStore schema fields are populated
        result.SourceFilePath.Should().NotBeNullOrWhiteSpace("SourceFilePath must be populated");
        result.SourceFilePath.Should().Be(filePath);
        result.ChunkIndex.Should().Be(chunkIndex, "ChunkIndex must match provided index");
        result.ContentType.Should().Be("code", "ContentType must be 'code' for code files");
        result.Language.Should().Be(expectedLanguage, $"Language must be correctly mapped from {extension}");
        result.LastModified.Should().NotBe(default(DateTimeOffset), "LastModified must not be default");
        result.LastModified.Should().Be(lastModified);
        // SectionHeading reflects namespace/class structure
        result.SectionHeading.Should().NotBeNullOrWhiteSpace(
            "SectionHeading should reflect namespace/class structure for code with symbols");
    }

    [Property(MaxTest = 100)]
    public void CodeMetadata_NamespaceAndClassPopulated_WhenSymbolsPresent(
        PositiveInt chunkIndexSeed)
    {
        // Arrange: code file with a namespace -> class -> method hierarchy
        var chunkIndex = chunkIndexSeed.Get % 5;
        var filePath = "/repo/src/Controllers/UserController.cs";
        var lastModified = DateTimeOffset.UtcNow;
        var metadata = new SourceFileMetadata(filePath, "UserController.cs", ".cs", lastModified);

        var symbols = new List<CodeSymbol>
        {
            new("MyApp.Controllers", CodeSymbolKind.Class, null),  // Namespace as a class-kind symbol
            new("UserController", CodeSymbolKind.Class, "MyApp.Controllers"),
            new("GetUser", CodeSymbolKind.Method, "UserController"),
            new("UpdateUser", CodeSymbolKind.Method, "UserController"),
            new("DeleteUser", CodeSymbolKind.Method, "UserController")
        };

        var code = new ParsedCode(
            "namespace MyApp.Controllers { class UserController { void GetUser() {} void UpdateUser() {} void DeleteUser() {} } }",
            symbols, new List<string>(), metadata);

        // Act
        var result = _extractor.ExtractCodeMetadata(code, chunkIndex, null);

        // Assert: SectionHeading reflects the namespace/class/method hierarchy
        result.SectionHeading.Should().NotBeNullOrWhiteSpace();
        result.SectionHeading.Should().Contain("UserController",
            "SectionHeading should contain the class name");
    }

    [Property(MaxTest = 100)]
    public void CodeMetadata_ExplicitContainingSymbol_TakesPrecedence(
        PositiveInt chunkIndexSeed)
    {
        // Arrange
        var chunkIndex = chunkIndexSeed.Get % 10;
        var filePath = "/repo/src/Service.cs";
        var lastModified = DateTimeOffset.UtcNow;
        var metadata = new SourceFileMetadata(filePath, "Service.cs", ".cs", lastModified);

        var symbols = new List<CodeSymbol>
        {
            new("MyClass", CodeSymbolKind.Class, null),
            new("SomeMethod", CodeSymbolKind.Method, "MyClass")
        };

        var code = new ParsedCode("class MyClass { void SomeMethod() {} }", symbols, new List<string>(), metadata);
        var explicitSymbol = "ExplicitNamespace.ExplicitClass.ExplicitMethod";

        // Act
        var result = _extractor.ExtractCodeMetadata(code, chunkIndex, explicitSymbol);

        // Assert: When an explicit containing symbol is provided, it takes precedence
        result.SectionHeading.Should().Be(explicitSymbol);
    }

    [Property(MaxTest = 100)]
    public void CodeMetadata_NoSymbols_SectionHeadingIsNull(
        PositiveInt chunkIndexSeed)
    {
        // Arrange: code file with no symbols
        var chunkIndex = chunkIndexSeed.Get % 10;
        var filePath = "/repo/src/Empty.cs";
        var lastModified = DateTimeOffset.UtcNow;
        var metadata = new SourceFileMetadata(filePath, "Empty.cs", ".cs", lastModified);

        var code = new ParsedCode("// empty file", new List<CodeSymbol>(), new List<string>(), metadata);

        // Act
        var result = _extractor.ExtractCodeMetadata(code, chunkIndex, null);

        // Assert: No symbols means SectionHeading is null
        result.SectionHeading.Should().BeNull(
            "SectionHeading should be null when no symbols are available");
        // But other fields are still populated
        result.SourceFilePath.Should().NotBeNullOrWhiteSpace();
        result.ContentType.Should().Be("code");
        result.Language.Should().Be("csharp");
        result.LastModified.Should().NotBe(default(DateTimeOffset));
    }

    [Property(MaxTest = 100)]
    public void CodeMetadata_ComponentSymbols_ResolvedCorrectly(
        PositiveInt chunkIndexSeed)
    {
        // Arrange: React component file
        var chunkIndex = chunkIndexSeed.Get % 3;
        var filePath = "/repo/src/components/UserProfile.tsx";
        var lastModified = DateTimeOffset.UtcNow;
        var metadata = new SourceFileMetadata(filePath, "UserProfile.tsx", ".tsx", lastModified);

        var symbols = new List<CodeSymbol>
        {
            new("UserProfile", CodeSymbolKind.Component, null),
            new("useUserData", CodeSymbolKind.Hook, "UserProfile"),
            new("useAuth", CodeSymbolKind.Hook, "UserProfile")
        };

        var code = new ParsedCode(
            "const UserProfile = () => { const data = useUserData(); const auth = useAuth(); }",
            symbols, new List<string>(), metadata);

        // Act
        var result = _extractor.ExtractCodeMetadata(code, chunkIndex, null);

        // Assert
        result.SourceFilePath.Should().NotBeNullOrWhiteSpace();
        result.ContentType.Should().Be("code");
        result.Language.Should().Be("typescript");
        result.LastModified.Should().NotBe(default(DateTimeOffset));
        result.SectionHeading.Should().NotBeNullOrWhiteSpace();
        result.SectionHeading.Should().Contain("UserProfile",
            "SectionHeading should reference the component name");
    }

    #endregion

    #region Helpers

    private static List<Heading> GenerateHeadings(int count)
    {
        var headingTexts = new[] { "Introduction", "Overview", "Details", "Summary", "Conclusion" };
        var headings = new List<Heading>();

        for (int i = 0; i < count; i++)
        {
            var level = Math.Min(i + 1, 6); // H1 through H6
            headings.Add(new Heading(level, headingTexts[i % headingTexts.Length]));
        }

        return headings;
    }

    private static string GenerateDocumentTextWithHeadings(List<Heading> headings)
    {
        var parts = new List<string>();
        foreach (var heading in headings)
        {
            parts.Add(heading.Text);
            parts.Add($"Content under {heading.Text}. This is a paragraph of text that follows the heading.");
        }
        parts.Add("Final paragraph content at the end of the document.");
        return string.Join("\n\n", parts);
    }

    #endregion
}
