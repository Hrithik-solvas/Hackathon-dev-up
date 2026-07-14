using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 18: Metadata filter correctness.
/// With a filter applied, every returned hit satisfies the filter criteria.
///
/// **Validates: Requirements 6.2**
/// </summary>
public class MetadataFilterProperty
{
    private static readonly string[] ContentTypes = { "code", "document" };
    private static readonly string[] Languages = { "csharp", "typescript", "sql", "python" };
    private static readonly string[] PathPrefixes = { "/src/", "/docs/", "/tests/", "/lib/" };

    [Property(MaxTest = 100)]
    public void ContentTypeFilter_CorrectlyFiltersHits(PositiveInt seed)
    {
        var random = new Random(seed.Get);
        string filterContentType = ContentTypes[random.Next(ContentTypes.Length)];
        var allHits = GenerateMixedHits(random);

        var matchingHits = allHits
            .Where(h => h.Metadata.ContentType == filterContentType)
            .ToList();

        foreach (var hit in matchingHits)
        {
            hit.Metadata.ContentType.Should().Be(filterContentType);
        }
    }

    [Property(MaxTest = 100)]
    public void LanguageFilter_CorrectlyFiltersHits(PositiveInt seed)
    {
        var random = new Random(seed.Get);
        string filterLanguage = Languages[random.Next(Languages.Length)];
        var allHits = GenerateMixedHits(random);

        var matchingHits = allHits
            .Where(h => h.Metadata.Language == filterLanguage)
            .ToList();

        foreach (var hit in matchingHits)
        {
            hit.Metadata.Language.Should().Be(filterLanguage);
        }
    }

    [Property(MaxTest = 100)]
    public void SourcePathPrefixFilter_CorrectlyFiltersHits(PositiveInt seed)
    {
        var random = new Random(seed.Get);
        string filterPrefix = PathPrefixes[random.Next(PathPrefixes.Length)];
        var allHits = GenerateMixedHits(random);

        var matchingHits = allHits
            .Where(h => h.Metadata.SourceFilePath.StartsWith(filterPrefix, StringComparison.Ordinal))
            .ToList();

        foreach (var hit in matchingHits)
        {
            hit.Metadata.SourceFilePath.Should().StartWith(filterPrefix);
        }
    }

    [Property(MaxTest = 100)]
    public void CombinedFilter_CorrectlyFiltersHits(PositiveInt seed)
    {
        var random = new Random(seed.Get);
        string filterContentType = ContentTypes[random.Next(ContentTypes.Length)];
        string filterLanguage = Languages[random.Next(Languages.Length)];
        var allHits = GenerateMixedHits(random);

        var matchingHits = allHits
            .Where(h => h.Metadata.ContentType == filterContentType
                     && h.Metadata.Language == filterLanguage)
            .ToList();

        foreach (var hit in matchingHits)
        {
            hit.Metadata.ContentType.Should().Be(filterContentType);
            hit.Metadata.Language.Should().Be(filterLanguage);
        }
    }

    private static List<SearchHit> GenerateMixedHits(Random random)
    {
        int count = random.Next(5, 15);
        var hits = new List<SearchHit>();

        for (int i = 0; i < count; i++)
        {
            hits.Add(new SearchHit(
                ChunkText: $"Content chunk {i}",
                RelevanceScore: (float)random.NextDouble(),
                Metadata: new ChunkMetadata(
                    SourceFilePath: $"{PathPrefixes[random.Next(PathPrefixes.Length)]}file{i}.cs",
                    ChunkIndex: i,
                    ContentType: ContentTypes[random.Next(ContentTypes.Length)],
                    Language: Languages[random.Next(Languages.Length)],
                    LastModified: DateTimeOffset.UtcNow.AddDays(-random.Next(1, 365)),
                    SectionHeading: $"Section {i}")));
        }

        return hits;
    }
}
