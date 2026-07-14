using System.Diagnostics;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;

namespace CodeCompassProject.CodeCompass.Repository.Services.Fakes;

/// <summary>
/// Fake pipeline orchestrator for development mode.
/// Simulates ingestion without actually processing files or calling Azure services.
/// </summary>
public class FakePipelineOrchestrator : IPipelineOrchestrator
{
    public Task<PipelineResult> RunAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            throw new ArgumentException("Target path cannot be null or empty.");
        }

        if (!Directory.Exists(request.TargetPath))
        {
            throw new DirectoryNotFoundException($"Target path does not exist: {request.TargetPath}");
        }

        // Count actual files in the directory for realistic response
        var mdFiles = Directory.GetFiles(request.TargetPath, "*.md", SearchOption.AllDirectories);
        var fileCount = mdFiles.Length > 0 ? mdFiles.Length : 1;

        var result = new PipelineResult(
            TotalFilesProcessed: fileCount,
            TotalChunksGenerated: fileCount * 8, // ~8 chunks per file on average
            TotalErrors: 0,
            ElapsedMilliseconds: fileCount * 150, // simulate ~150ms per file
            FilesNewlyIndexed: fileCount,
            FilesReIndexed: 0,
            FilesDeleted: 0,
            FilesSkipped: 0,
            FilesFailed: 0);

        return Task.FromResult(result);
    }
}
