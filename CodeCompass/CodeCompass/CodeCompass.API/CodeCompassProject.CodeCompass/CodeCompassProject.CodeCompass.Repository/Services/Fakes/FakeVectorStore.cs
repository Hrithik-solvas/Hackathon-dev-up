using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Domain.Entities;

namespace CodeCompassProject.CodeCompass.Repository.Services.Fakes;

/// <summary>
/// Fake vector store for development mode.
/// Used by legacy handlers (IngestDocumentsHandler, IngestCodeHandler, GetHealthHandler).
/// </summary>
public class FakeVectorStore : IVectorStore
{
    private readonly List<DocumentChunk> _chunks = new();

    public Task StoreAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        _chunks.AddRange(chunks);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DocumentChunk>> SearchAsync(float[] embedding, int topK, CancellationToken cancellationToken = default)
    {
        var results = _chunks.Take(topK);
        return Task.FromResult(results);
    }
}
