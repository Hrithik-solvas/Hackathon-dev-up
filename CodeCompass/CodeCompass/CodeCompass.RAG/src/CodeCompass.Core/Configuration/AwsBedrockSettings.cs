namespace CodeCompass.Core.Configuration;

/// <summary>
/// Configuration settings for AWS Bedrock embedding service.
/// </summary>
public record AwsBedrockSettings(
    string Region,
    string ModelId,
    string ChatModelId,
    int EmbeddingDimension = 1024)
{
    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public AwsBedrockSettings()
        : this("ap-southeast-2", "amazon.titan-embed-text-v2:0", "anthropic.claude-3-haiku-20240307-v1:0")
    {
    }
}
