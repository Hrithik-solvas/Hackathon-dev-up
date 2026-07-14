using CodeCompassProject.CodeCompass.Domain.Entities;
using AppVectorStore = CodeCompassProject.CodeCompass.Application.Interfaces.IVectorStore;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Bridges the legacy API vector-store interface to the real RAG vector store used by the pipeline.
/// Keeps a small in-memory index for legacy health checks while persisting all ingested chunks to OpenSearch.
/// </summary>
public class RagVectorStoreAdapter : AppVectorStore
{
    private readonly global::CodeCompass.Core.Interfaces.IVectorStore _ragVectorStore;
    private readonly List<DocumentChunk> _localChunks = new();

    public RagVectorStoreAdapter(global::CodeCompass.Core.Interfaces.IVectorStore ragVectorStore)
    {
        _ragVectorStore = ragVectorStore;
    }

    public async Task StoreAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
        {
            return;
        }

        var documents = new List<global::CodeCompass.Core.Models.VectorDocument>(chunkList.Count);
        for (var i = 0; i < chunkList.Count; i++)
        {
            var chunk = chunkList[i];
            _localChunks.Add(CloneChunk(chunk));

            documents.Add(new global::CodeCompass.Core.Models.VectorDocument(
                Id: Guid.NewGuid().ToString("N"),
                Embedding: chunk.EmbeddingVector,
                ChunkText: chunk.Content,
                Metadata: new global::CodeCompass.Core.Models.ChunkMetadata(
                    SourceFilePath: chunk.SourceUri,
                    ChunkIndex: i,
                    ContentType: chunk.SourceType == SourceType.Code ? "code" : "document",
                    Language: TryGetMetadataValue(chunk.Metadata, "language"),
                    LastModified: chunk.CreatedAt,
                    SectionHeading: TryGetMetadataValue(chunk.Metadata, "sectionHeading"))));
        }

        await _ragVectorStore.UpsertAsync(documents, cancellationToken).ConfigureAwait(false);
    }

    public Task<IEnumerable<DocumentChunk>> SearchAsync(float[] embedding, int topK, CancellationToken cancellationToken = default)
    {
        if (_localChunks.Count == 0 || topK <= 0)
        {
            return Task.FromResult(Enumerable.Empty<DocumentChunk>());
        }

        var results = _localChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CosineSimilarity(embedding, chunk.EmbeddingVector)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => CloneChunk(x.Chunk));

        return Task.FromResult(results);
    }

    private static DocumentChunk CloneChunk(DocumentChunk chunk)
    {
        return new DocumentChunk
        {
            Id = chunk.Id,
            Content = chunk.Content,
            SourceUri = chunk.SourceUri,
            SourceType = chunk.SourceType,
            EmbeddingVector = chunk.EmbeddingVector.ToArray(),
            CreatedAt = chunk.CreatedAt,
            Metadata = new Dictionary<string, string>(chunk.Metadata)
        };
    }

    private static string? TryGetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0.0;
        }

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