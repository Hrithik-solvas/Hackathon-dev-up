using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Validates that all required configuration values are present and non-empty at startup.
/// Throws an InvalidOperationException with a descriptive message if any value is missing or empty.
/// </summary>
public class ConfigurationValidationHostedService : IHostedService
{
    private readonly IConfiguration _configuration;

    private static readonly string[] RequiredConfigurationKeys =
    [
        "AzureOpenAI:Endpoint",
        "AzureOpenAI:ApiKey",
        "AzureOpenAI:DeploymentName",
        "AzureOpenAI:EmbeddingDeploymentName",
        "AzureSearch:Endpoint",
        "AzureSearch:ApiKey",
        "AzureSearch:IndexName",
        "KnowledgeBases:Product:SourcePathPrefix",
        "KnowledgeBases:TechStack:SourcePathPrefix"
    ];

    public ConfigurationValidationHostedService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missingKeys = new List<string>();

        foreach (var key in RequiredConfigurationKeys)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missingKeys.Add(key);
            }
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"The following required configuration values are missing or empty: {string.Join(", ", missingKeys)}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
