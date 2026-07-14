using CodeCompass.Core.Interfaces;

namespace CodeCompassProject.CodeCompass.Repository.Services.Fakes;

/// <summary>
/// Fake embedding generator for development mode.
/// Returns deterministic fake embeddings without calling Azure OpenAI.
/// </summary>
public class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    private const int EmbeddingDimension = 1536; // Same as text-embedding-ada-002

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = GenerateDeterministicEmbedding(text);
        return Task.FromResult(embedding);
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = texts.Select(GenerateDeterministicEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    private static float[] GenerateDeterministicEmbedding(string text)
    {
        // Generate a deterministic embedding based on text hash
        var hash = text.GetHashCode();
        var rng = new Random(hash);
        var embedding = new float[EmbeddingDimension];
        for (int i = 0; i < EmbeddingDimension; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1); // Range [-1, 1]
        }

        // Normalize to unit vector
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < EmbeddingDimension; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
