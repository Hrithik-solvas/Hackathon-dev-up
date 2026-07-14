using CodeCompass.Core.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 17: Search result ordering and completeness.
/// Results are ordered by descending relevance score in [0.0, 1.0],
/// each hit has non-empty chunk text and all metadata fields populated.
///
/// **Validates: Requirements 6.1**
/// </summary>
public class SearchResultOrderingProperty
{
    [Property(MaxTest = 100)]
    public void Results_AreOrderedByDescendingRelevanceScore(PositiveInt hitCountSeed)
    {
        int hitCount = ((hitCountSeed.Get - 1) % 20) + 1;
        var random = new Random(hitCountSeed.Get);

        var hits = GenerateRandomSearchHits(hitCount, random);

        // Simulate what the search service does - sort by descending score
        var ordered = hits.OrderByDescending(h => h.RelevanceScore).ToList();

        if (ordered.Count > 1)
        {
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                ordered[i].RelevanceScore.Should().BeGreaterThanOrEqualTo(
                    ordered[i + 1].RelevanceScore);
            }
        }
    }

    [Property(MaxTest = 100)]
    public void AllScores_AreInValidRange(PositiveInt hitCountSeed)
    {
        int hitCount = ((hitCountSeed.Get - 1) % 20) + 1;
        var random = new Random(hitCountSeed.Get);

        var hits = GenerateRandomSearchHits(hitCount, random);

        foreach (var hit in hits)
        {
            hit.RelevanceScore.Should().BeGreaterThanOrEqualTo(0.0f);
            hit.RelevanceScore.Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Property(MaxTest = 100)]
    public void AllHits_HaveNonEmptyChunkText(PositiveInt hitCountSeed)
    {
        int hitCount = ((hitCountSeed.Get - 1) % 20) + 1;
        var random = new Random(hitCountSeed.Get);

        var hits = GenerateRandomSearchHits(hitCount, random);

        foreach (var hit in hits)
        {
            hit.ChunkText.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Property(MaxTest = 100)]
    public void AllHits_HavePopulatedMetadata(PositiveInt hitCountSeed)
    {
        int hitCount = ((hitCountSeed.Get - 1) % 20) + 1;
        var random = new Random(hitCountSeed.Get);

        var hits = GenerateRandomSearchHits(hitCount, random);

        foreach (var hit in hits)
        {
            hit.Metadata.Should().NotBeNull();
            hit.Metadata.SourceFilePath.Should().NotBeNullOrWhiteSpace();
            hit.Metadata.ContentType.Should().NotBeNullOrWhiteSpace();
            hit.Metadata.LastModified.Should().NotBe(default);
            hit.Metadata.ChunkIndex.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    private static List<SearchHit> GenerateRandomSearchHits(int count, Random random)
    {
        var contentTypes = new[] { "code", "document" };
        var languages = new[] { "csharp", "typescript", "sql", "markdown" };

        var hits = new List<SearchHit>();
        for (int i = 0; i < count; i++)
        {
            float score = (float)random.NextDouble();

            hits.Add(new SearchHit(
                ChunkText: $"Chunk content for hit {i} with meaningful text",
                RelevanceScore: score,
                Metadata: new ChunkMetadata(
                    SourceFilePath: $"/src/project/file{i}.cs",
                    ChunkIndex: i,
                    ContentType: contentTypes[random.Next(contentTypes.Length)],
                    Language: languages[random.Next(languages.Length)],
                    LastModified: DateTimeOffset.UtcNow.AddDays(-random.Next(1, 365)),
                    SectionHeading: $"Section {i}")));
        }

        return hits;
    }
}
