using CodeCompassProject.CodeCompass.Domain.Entities;

namespace CodeCompassProject.CodeCompass.Application.Interfaces;

public interface IDocumentIngestionService
{
    Task<IEnumerable<DocumentChunk>> ChunkDocumentAsync(Stream content, string sourceUri, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentChunk>> ChunkCodeAsync(Stream content, string sourceUri, CancellationToken cancellationToken = default);
}
