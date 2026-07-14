namespace CodeCompass.Embedding;

/// <summary>
/// Internal abstraction over the Azure OpenAI EmbeddingClient for testability.
/// </summary>
internal interface IEmbeddingClientWrapper
{
    /// <summary>
    /// Generates a single embedding for the given text.
    /// </summary>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for a batch of texts.
    /// </summary>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(
        IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
