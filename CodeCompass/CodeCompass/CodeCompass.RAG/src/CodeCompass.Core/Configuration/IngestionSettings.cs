namespace CodeCompass.Core.Configuration;

/// <summary>
/// Configuration settings for pipeline ingestion behavior.
/// </summary>
public record IngestionSettings(
    int ConcurrencyLevel = 4,
    int EmbeddingBatchSize = 16,
    int MaxFileSizeMB = 50)
{
    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public IngestionSettings()
        : this(4, 16, 50)
    {
    }
}
