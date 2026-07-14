using CodeCompass.Core.Configuration;
using CodeCompass.Core.Models;
using CodeCompass.Parsing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 3: Markdown heading extraction.
/// For any Markdown document containing heading markers (# through ######),
/// the parser extracts each heading with its correct level (1–6) and text content,
/// and the number of extracted headings equals the number of heading markers in the source.
///
/// **Validates: Requirements 1.1**
/// </summary>
public class MarkdownHeadingExtractionProperty
{
    private static readonly IngestionSettings DefaultSettings = new(ConcurrencyLevel: 4, EmbeddingBatchSize: 16, MaxFileSizeMB: 50);
    private static readonly IOptions<IngestionSettings> SettingsOptions = Options.Create(DefaultSettings);
    private static readonly FileValidator Validator = new(SettingsOptions);

    private static MarkdownParser CreateMarkdownParser() =>
        new(NullLogger<MarkdownParser>.Instance, Validator);

    [Property(MaxTest = 100)]
    public void Parser_ExtractsCorrectHeadingLevelsAndText_AndCountMatchesSource(PositiveInt headingCountSeed, PositiveInt bodySeed)
    {
        var headingCount = (headingCountSeed.Get % 10) + 1; // 1-10 headings
        var bodyLineCount = bodySeed.Get % 5; // 0-4 body lines between headings

        var expectedHeadings = GenerateExpectedHeadings(headingCount);
        var content = BuildMarkdownContent(expectedHeadings, bodyLineCount);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");

        try
        {
            File.WriteAllText(tempFile, content);
            var parser = CreateMarkdownParser();
            var result = parser.ParseAsync(tempFile).GetAwaiter().GetResult();

            // Property: heading count equals marker count in source
            result.Headings.Count.Should().Be(expectedHeadings.Count,
                "the number of extracted headings must equal the number of heading markers in the source");

            // Property: each heading has the correct level and text
            for (var i = 0; i < expectedHeadings.Count; i++)
            {
                result.Headings[i].Level.Should().Be(expectedHeadings[i].Level,
                    $"heading at index {i} should have correct level");
                result.Headings[i].Text.Should().Be(expectedHeadings[i].Text,
                    $"heading at index {i} should have correct text");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Property(MaxTest = 100)]
    public void Parser_ExtractsAllHeadingLevels_OneThrough_Six(PositiveInt seed)
    {
        // Generate a document that includes all 6 heading levels
        var variant = seed.Get % 5;
        var expectedHeadings = new List<(int Level, string Text)>();

        for (var level = 1; level <= 6; level++)
        {
            var text = $"Level {level} heading variant {variant}";
            expectedHeadings.Add((level, text));
        }

        var lines = new List<string>();
        foreach (var (level, text) in expectedHeadings)
        {
            lines.Add($"{new string('#', level)} {text}");
            lines.Add(string.Empty);
            lines.Add("Some body text paragraph.");
            lines.Add(string.Empty);
        }

        var content = string.Join("\n", lines);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");

        try
        {
            File.WriteAllText(tempFile, content);
            var parser = CreateMarkdownParser();
            var result = parser.ParseAsync(tempFile).GetAwaiter().GetResult();

            result.Headings.Count.Should().Be(6,
                "document with all 6 levels should produce 6 headings");

            for (var i = 0; i < 6; i++)
            {
                result.Headings[i].Level.Should().Be(expectedHeadings[i].Level,
                    $"heading {i} should have level {expectedHeadings[i].Level}");
                result.Headings[i].Text.Should().Be(expectedHeadings[i].Text,
                    $"heading {i} text should match source");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Generates a list of expected headings with varying levels and unique text.
    /// </summary>
    private static List<Heading> GenerateExpectedHeadings(int count)
    {
        var headings = new List<Heading>();
        var headingTexts = new[]
        {
            "Introduction", "Getting Started", "Architecture", "Implementation",
            "Testing", "Deployment", "Monitoring", "Conclusion",
            "References", "Appendix"
        };

        for (var i = 0; i < count; i++)
        {
            var level = (i % 6) + 1; // Cycles through levels 1-6
            var text = $"{headingTexts[i % headingTexts.Length]} Section {i + 1}";
            headings.Add(new Heading(level, text));
        }

        return headings;
    }

    /// <summary>
    /// Builds Markdown content from expected headings, interleaving body lines.
    /// </summary>
    private static string BuildMarkdownContent(List<Heading> headings, int bodyLinesPerHeading)
    {
        var lines = new List<string>();
        var bodyTexts = new[]
        {
            "This is a paragraph of text.", "Another paragraph follows.",
            "Details about the topic.", "Further explanation here."
        };

        foreach (var heading in headings)
        {
            lines.Add($"{new string('#', heading.Level)} {heading.Text}");
            lines.Add(string.Empty);

            for (var j = 0; j < bodyLinesPerHeading; j++)
            {
                lines.Add(bodyTexts[j % bodyTexts.Length]);
                lines.Add(string.Empty);
            }
        }

        return string.Join("\n", lines);
    }
}
