using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Manages change detection and state tracking for incremental indexing.
/// </summary>
public interface IIncrementalIndexingService
{
    Task<IndexingPlan> ComputePlanAsync(string targetPath, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(string filePath, DateTimeOffset lastModified, int chunkCount = 0, CancellationToken cancellationToken = default);
    Task RemoveStateAsync(string filePath, CancellationToken cancellationToken = default);
}
