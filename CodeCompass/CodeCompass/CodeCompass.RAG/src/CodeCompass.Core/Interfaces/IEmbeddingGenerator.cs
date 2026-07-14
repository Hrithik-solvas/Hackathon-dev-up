namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Converts text chunks into vector embeddings via Azure OpenAI.
/// </summary>
public interface IEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
