using CodeCompassProject.CodeCompass.Domain.Entities;

namespace CodeCompassProject.CodeCompass.Application.Interfaces;

public interface IVectorStore
{
    Task StoreAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentChunk>> SearchAsync(float[] embedding, int topK, CancellationToken cancellationToken = default);
}
