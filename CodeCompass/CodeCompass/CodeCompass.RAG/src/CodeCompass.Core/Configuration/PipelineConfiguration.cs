using CodeCompass.Core.Models;

namespace CodeCompass.Core.Configuration;

/// <summary>
/// Top-level configuration for the RAG pipeline, binding all subsections.
/// </summary>
public record PipelineConfiguration(
    AwsBedrockSettings AwsBedrock,
    OpenSearchSettings OpenSearch,
    ChunkingOptions Chunking,
    IngestionSettings Ingestion)
{
    /// <summary>
    /// The configuration section name used for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public PipelineConfiguration()
        : this(new AwsBedrockSettings(), new OpenSearchSettings(), new ChunkingOptions(), new IngestionSettings())
    {
    }
}
