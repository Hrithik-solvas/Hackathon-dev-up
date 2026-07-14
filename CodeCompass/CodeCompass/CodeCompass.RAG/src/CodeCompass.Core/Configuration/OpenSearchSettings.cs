namespace CodeCompass.Core.Configuration;

/// <summary>
/// Configuration settings for Amazon OpenSearch Service.
/// </summary>
public record OpenSearchSettings(
    string Endpoint,
    string IndexName,
    string Region,
    int BatchSize = 100)
{
    /// <summary>
    /// Parameterless constructor for configuration binding.
    /// </summary>
    public OpenSearchSettings()
        : this(string.Empty, "codecompass-index", "ap-southeast-2")
    {
    }
}
