using CodeCompass.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCompass.Core.Configuration;

/// <summary>
/// Extension methods for registering pipeline configuration with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers pipeline configuration sections with the DI container using the IOptions pattern.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration root containing the Pipeline section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPipelineConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(PipelineConfiguration.SectionName);

        services.Configure<PipelineConfiguration>(section);
        services.Configure<AwsBedrockSettings>(section.GetSection(nameof(PipelineConfiguration.AwsBedrock)));
        services.Configure<OpenSearchSettings>(section.GetSection(nameof(PipelineConfiguration.OpenSearch)));
        services.Configure<ChunkingOptions>(section.GetSection(nameof(PipelineConfiguration.Chunking)));
        services.Configure<IngestionSettings>(section.GetSection(nameof(PipelineConfiguration.Ingestion)));

        return services;
    }
}
