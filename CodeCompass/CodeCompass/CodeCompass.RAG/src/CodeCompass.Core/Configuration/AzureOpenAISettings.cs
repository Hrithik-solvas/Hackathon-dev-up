namespace CodeCompass.Core.Configuration;

/// <summary>
/// Configuration settings for Azure OpenAI embedding service.
/// </summary>
public record AzureOpenAISettings(
    string Endpoint,
    string DeploymentName,
    string ApiKey,
    int EmbeddingDimension = 1536)
{
    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public AzureOpenAISettings()
        : this(string.Empty, string.Empty, string.Empty)
    {
    }
}
