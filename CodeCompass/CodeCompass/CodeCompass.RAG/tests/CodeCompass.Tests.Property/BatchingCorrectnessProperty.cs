using CodeCompass.Embedding;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 12: Batching correctness.
/// For any list of N items and batch size B, batching produces ⌈N/B⌉ groups
/// each with ≤ B items, every item in exactly one batch, order preserved.
///
/// **Validates: Requirements 4.2, 9.3, 9.4**
/// </summary>
public class BatchingCorrectnessProperty
{
    [Property(MaxTest = 100)]
    public void Batching_ProducesCorrectNumberOfGroups(PositiveInt itemCountSeed, PositiveInt batchSizeSeed)
    {
        // Constrain N to [1, 200] and B to [1, 50]
        int itemCount = (itemCountSeed.Get % 200) + 1;
        int batchSize = (batchSizeSeed.Get % 50) + 1;

        var items = Enumerable.Range(0, itemCount).Select(i => $"item_{i}").ToList();
        var generator = CreateGenerator(batchSize);

        // Act
        var batches = generator.CreateBatches(items);

        // Assert - should produce exactly ⌈N/B⌉ groups
        int expectedBatchCount = (int)Math.Ceiling((double)itemCount / batchSize);
        batches.Should().HaveCount(expectedBatchCount,
            $"N={itemCount}, B={batchSize} should produce ⌈{itemCount}/{batchSize}⌉ = {expectedBatchCount} batches");
    }

    [Property(MaxTest = 100)]
    public void Batching_EachGroupHasAtMostBItems(PositiveInt itemCountSeed, PositiveInt batchSizeSeed)
    {
        int itemCount = (itemCountSeed.Get % 200) + 1;
        int batchSize = (batchSizeSeed.Get % 50) + 1;

        var items = Enumerable.Range(0, itemCount).Select(i => $"item_{i}").ToList();
        var generator = CreateGenerator(batchSize);

        // Act
        var batches = generator.CreateBatches(items);

        // Assert - each group has ≤ B items and is non-empty
        foreach (var batch in batches)
        {
            batch.Count.Should().BeLessThanOrEqualTo(batchSize,
                $"each batch should have at most {batchSize} items");
            batch.Count.Should().BeGreaterThan(0, "no batch should be empty");
        }
    }

    [Property(MaxTest = 100)]
    public void Batching_EveryItemAppearsExactlyOnce(PositiveInt itemCountSeed, PositiveInt batchSizeSeed)
    {
        int itemCount = (itemCountSeed.Get % 200) + 1;
        int batchSize = (batchSizeSeed.Get % 50) + 1;

        var items = Enumerable.Range(0, itemCount).Select(i => $"item_{i}").ToList();
        var generator = CreateGenerator(batchSize);

        // Act
        var batches = generator.CreateBatches(items);

        // Assert - flatten all batches and compare to original list
        var allItems = batches.SelectMany(b => b).ToList();
        allItems.Should().HaveCount(itemCount, "total items across all batches should equal input count");

        // Each item appears exactly once (since items are unique strings)
        var allItemsSet = new HashSet<string>(allItems);
        allItemsSet.Should().HaveCount(itemCount, "every item should appear exactly once across all batches");
    }

    [Property(MaxTest = 100)]
    public void Batching_OrderIsPreserved(PositiveInt itemCountSeed, PositiveInt batchSizeSeed)
    {
        int itemCount = (itemCountSeed.Get % 200) + 1;
        int batchSize = (batchSizeSeed.Get % 50) + 1;

        var items = Enumerable.Range(0, itemCount).Select(i => $"item_{i}").ToList();
        var generator = CreateGenerator(batchSize);

        // Act
        var batches = generator.CreateBatches(items);

        // Assert - concatenating all batches produces the original list
        var concatenated = batches.SelectMany(b => b).ToList();
        concatenated.Should().Equal(items, "concatenating all batches in order should reproduce the original list");
    }

    /// <summary>
    /// Creates a BedrockEmbeddingGenerator with a fake Bedrock client for testing batch logic.
    /// </summary>
    private static BedrockEmbeddingGenerator CreateGenerator(int batchSize)
    {
        // Use the internal constructor that accepts a client directly.
        // We pass null for the client since CreateBatches doesn't use it.
        return new BedrockEmbeddingGenerator(
            client: null!,
            modelId: "amazon.titan-embed-text-v2:0",
            batchSize: batchSize,
            logger: NullLogger<BedrockEmbeddingGenerator>.Instance,
            retryPolicy: new CodeCompass.Core.Resilience.RetryPolicy(NullLogger.Instance));
    }
}
