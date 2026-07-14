using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Executes semantic similarity queries against the vector index.
/// </summary>
public interface IVectorSearch
{
    Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
