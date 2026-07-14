namespace CodeCompass.Core.Configuration;

/// <summary>
/// Configuration settings for Azure AI Search service.
/// </summary>
public record AzureSearchSettings(
    string Endpoint,
    string IndexName,
    string ApiKey,
    int BatchSize = 100)
{
    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public AzureSearchSettings()
        : this(string.Empty, string.Empty, string.Empty)
    {
    }
}
