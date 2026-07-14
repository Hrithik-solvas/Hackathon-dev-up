using CodeCompass.Search;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 14: Deterministic document ID generation.
/// For any source file path and chunk index pair, the ID is deterministic
/// (identical inputs → identical IDs) and collision-resistant (distinct inputs → distinct IDs).
///
/// **Validates: Requirements 5.2**
/// </summary>
public class DeterministicDocumentIdProperty
{
    [Property(MaxTest = 100)]
    public void IdenticalInputs_ProduceIdenticalIds(NonEmptyString pathSeed, NonNegativeInt chunkIndexSeed)
    {
        // Arrange
        string sourceFilePath = pathSeed.Get;
        int chunkIndex = chunkIndexSeed.Get;

        // Act
        string id1 = OpenSearchVectorStore.GenerateDocumentId(sourceFilePath, chunkIndex);
        string id2 = OpenSearchVectorStore.GenerateDocumentId(sourceFilePath, chunkIndex);

        // Assert - determinism: same inputs always produce same output
        id1.Should().Be(id2,
            $"GenerateDocumentId should be deterministic for path='{sourceFilePath}', index={chunkIndex}");
    }

    [Property(MaxTest = 100)]
    public void DistinctInputs_ProduceDistinctIds(
        NonEmptyString path1Seed, NonNegativeInt index1Seed,
        NonEmptyString path2Seed, NonNegativeInt index2Seed)
    {
        // Arrange
        string path1 = path1Seed.Get;
        int index1 = index1Seed.Get;
        string path2 = path2Seed.Get;
        int index2 = index2Seed.Get;

        // Only test when inputs are actually distinct
        if (path1 == path2 && index1 == index2)
            return; // Skip identical inputs

        // Act
        string id1 = OpenSearchVectorStore.GenerateDocumentId(path1, index1);
        string id2 = OpenSearchVectorStore.GenerateDocumentId(path2, index2);

        // Assert - collision resistance: distinct inputs produce distinct IDs
        id1.Should().NotBe(id2,
            $"distinct inputs ('{path1}',{index1}) and ('{path2}',{index2}) should produce distinct IDs");
    }

    [Property(MaxTest = 100)]
    public void Output_IsValidBase64UrlString(NonEmptyString pathSeed, NonNegativeInt chunkIndexSeed)
    {
        // Arrange
        string sourceFilePath = pathSeed.Get;
        int chunkIndex = chunkIndexSeed.Get;

        // Act
        string id = OpenSearchVectorStore.GenerateDocumentId(sourceFilePath, chunkIndex);

        // Assert - Base64Url encoding must not contain +, /, or = characters
        id.Should().NotBeNullOrWhiteSpace("generated ID should not be empty");
        id.Should().NotContain("+", "Base64Url should not contain '+'");
        id.Should().NotContain("/", "Base64Url should not contain '/'");
        id.Should().NotContain("=", "Base64Url should not contain '='");

        // Should only contain valid Base64Url characters: A-Z, a-z, 0-9, -, _
        id.Should().MatchRegex(@"^[A-Za-z0-9\-_]+$",
            "ID should only contain valid Base64Url characters (A-Z, a-z, 0-9, -, _)");
    }
}
