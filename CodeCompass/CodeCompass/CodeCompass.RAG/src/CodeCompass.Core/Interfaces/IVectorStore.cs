using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Persists and manages vector embeddings in Azure AI Search.
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default);
    Task DeleteBySourceFileAsync(string sourceFilePath, CancellationToken cancellationToken = default);
}
