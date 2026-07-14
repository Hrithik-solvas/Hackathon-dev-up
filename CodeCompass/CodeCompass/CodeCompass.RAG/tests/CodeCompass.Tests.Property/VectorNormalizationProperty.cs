using CodeCompass.Embedding;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 13: Vector normalization to unit length.
/// For any embedding vector produced, the L2 norm equals 1.0 within floating-point
/// tolerance of ±1e-6. Zero vectors remain zero.
///
/// **Validates: Requirements 4.5**
/// </summary>
public class VectorNormalizationProperty
{
    /// <summary>
    /// Generates a non-zero vector of the given dimension using the seed and range type.
    /// </summary>
    private static float[] GenerateNonZeroVector(int dimension, int seed, int rangeType)
    {
        var rng = new Random(seed);
        var vector = new float[dimension];

        for (int i = 0; i < dimension; i++)
        {
            double value = rangeType switch
            {
                0 => rng.NextDouble() * 2.0 - 1.0,       // range [-1, 1]
                1 => rng.NextDouble() * 200.0 - 100.0,    // range [-100, 100]
                2 => rng.NextDouble() * 0.01 - 0.005,     // small values [-0.005, 0.005]
                _ => rng.NextDouble() * 10000.0 - 5000.0   // large values [-5000, 5000]
            };
            vector[i] = (float)value;
        }

        // Ensure at least one non-zero element
        if (vector.All(v => v == 0.0f))
        {
            vector[0] = 1.0f;
        }

        return vector;
    }

    [Property(MaxTest = 100)]
    public void NormalizedVector_HasL2NormOfOne(PositiveInt dimensionSeed, PositiveInt valueSeed, PositiveInt rangeTypeSeed)
    {
        // Constrain: dimensions 1-2048, 4 range types
        int dimension = (dimensionSeed.Get % 2048) + 1;
        int rangeType = rangeTypeSeed.Get % 4;
        var inputVector = GenerateNonZeroVector(dimension, valueSeed.Get, rangeType);

        // Act
        float[] normalized = BedrockEmbeddingGenerator.NormalizeVector(inputVector);

        // Assert - L2 norm should be 1.0 ± 1e-6
        double norm = Math.Sqrt(normalized.Sum(x => (double)x * x));
        norm.Should().BeApproximately(1.0, 1e-6,
            $"normalized vector of dimension {dimension} should have L2 norm = 1.0");
    }

    [Property(MaxTest = 100)]
    public void NormalizedVector_PreservesDirection(PositiveInt dimensionSeed, PositiveInt valueSeed, PositiveInt rangeTypeSeed)
    {
        int dimension = (dimensionSeed.Get % 2048) + 1;
        int rangeType = rangeTypeSeed.Get % 4;
        var inputVector = GenerateNonZeroVector(dimension, valueSeed.Get, rangeType);

        // Act
        float[] normalized = BedrockEmbeddingGenerator.NormalizeVector(inputVector);

        // Assert - all elements should maintain their sign (direction preserved)
        normalized.Should().HaveCount(inputVector.Length,
            "normalized vector should have the same dimensionality");

        for (int i = 0; i < inputVector.Length; i++)
        {
            if (inputVector[i] > 0)
                normalized[i].Should().BeGreaterThan(0,
                    $"element {i} should preserve positive sign");
            else if (inputVector[i] < 0)
                normalized[i].Should().BeLessThan(0,
                    $"element {i} should preserve negative sign");
            else
                normalized[i].Should().Be(0,
                    $"element {i} should remain zero if input was zero");
        }
    }

    [Property(MaxTest = 100)]
    public void ZeroVector_RemainsZero(PositiveInt dimensionSeed)
    {
        // Arrange - create zero vector of various dimensions (1-2048)
        int dimension = (dimensionSeed.Get % 2048) + 1;
        float[] zeroVector = new float[dimension];

        // Act
        float[] result = BedrockEmbeddingGenerator.NormalizeVector(zeroVector);

        // Assert - zero vector norm should be 0
        double norm = Math.Sqrt(result.Sum(x => (double)x * x));
        norm.Should().Be(0.0, "zero vector should remain zero after normalization");
        result.Should().AllSatisfy(v => v.Should().Be(0.0f));
    }

    [Property(MaxTest = 100)]
    public void NormalizedVector_IsIdempotent(PositiveInt dimensionSeed, PositiveInt valueSeed, PositiveInt rangeTypeSeed)
    {
        int dimension = (dimensionSeed.Get % 2048) + 1;
        int rangeType = rangeTypeSeed.Get % 4;
        var inputVector = GenerateNonZeroVector(dimension, valueSeed.Get, rangeType);

        // Normalizing an already-normalized vector should still yield norm = 1.0
        float[] firstNorm = BedrockEmbeddingGenerator.NormalizeVector(inputVector);
        float[] secondNorm = BedrockEmbeddingGenerator.NormalizeVector(firstNorm);

        double norm = Math.Sqrt(secondNorm.Sum(x => (double)x * x));
        norm.Should().BeApproximately(1.0, 1e-6,
            "normalizing a normalized vector should still produce unit length");
    }
}
