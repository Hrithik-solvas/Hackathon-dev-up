using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Top-level entry point that coordinates the full indexing pipeline.
/// </summary>
public interface IPipelineOrchestrator
{
    Task<PipelineResult> RunAsync(PipelineRequest request, CancellationToken cancellationToken = default);
}
