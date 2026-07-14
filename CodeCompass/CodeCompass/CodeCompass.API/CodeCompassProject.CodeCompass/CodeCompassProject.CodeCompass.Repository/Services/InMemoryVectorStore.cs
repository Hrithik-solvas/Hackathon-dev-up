using System.Collections.Concurrent;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// In-memory vector store for development and testing.
/// Replace with Azure AI Search, Qdrant, or Pinecone for production.
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentBag<DocumentChunk> _chunks = new();
    private readonly ILogger<InMemoryVectorStore> _logger;

    public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger)
    {
        _logger = logger;
    }

    public Task StoreAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            _chunks.Add(chunk);
        }

        _logger.LogDebug("Stored {Count} chunks. Total chunks in store: {Total}", chunks.Count(), _chunks.Count);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DocumentChunk>> SearchAsync(float[] embedding, int topK, CancellationToken cancellationToken = default)
    {
        if (!_chunks.Any())
        {
            return Task.FromResult(Enumerable.Empty<DocumentChunk>());
        }

        // Cosine similarity search
        var results = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CosineSimilarity(embedding, chunk.EmbeddingVector)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk);

        return Task.FromResult(results);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0.0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
