using CodeCompass.Chunking;
using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Resilience;
using CodeCompass.Embedding;
using CodeCompass.Indexing;
using CodeCompass.Parsing;
using CodeCompass.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Pipeline;

/// <summary>
/// Extension methods for registering all CodeCompass pipeline services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all CodeCompass pipeline services, binds configuration from appsettings.json,
    /// and wires up logging.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration root containing pipeline settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCodeCompassPipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind configuration sections to strongly-typed options
        services.Configure<AwsBedrockSettings>(configuration.GetSection("AwsBedrock"));
        services.Configure<OpenSearchSettings>(configuration.GetSection("OpenSearch"));
        services.Configure<IngestionSettings>(configuration.GetSection("Ingestion"));

        // 2. Add logging
        services.AddLogging();

        // 3. Register RetryPolicy (Singleton) - requires ILogger
        services.AddSingleton<RetryPolicy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RetryPolicy>>();
            return new RetryPolicy(logger);
        });

        // 4. Register document parsers (Singleton)
        services.AddSingleton<FileValidator>();
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, PdfParser>();
        services.AddSingleton<IDocumentParser, DocxParser>();

        // 5. Register code parsers (Singleton)
        services.AddSingleton<ICodeParser, CSharpCodeParser>();
        services.AddSingleton<ICodeParser, ReactCodeParser>();
        services.AddSingleton<ICodeParser, SqlCodeParser>();

        // 6. Register repository enumerator (Scoped)
        services.AddScoped<IRepositoryEnumerator, RepositoryEnumerator>();

        // 7. Register chunking service (Scoped)
        services.AddScoped<IChunkingService, ChunkingService>();

        // 8. Register metadata extractor (Scoped)
        services.AddScoped<IMetadataExtractor, MetadataExtractor>();

        // 9. Register embedding generator (Singleton) - AWS Bedrock
        services.AddSingleton<IEmbeddingGenerator, BedrockEmbeddingGenerator>();

        // 10. Register vector store (Singleton) - Amazon OpenSearch
        services.AddSingleton<IVectorStore, OpenSearchVectorStore>();

        // 11. Register vector search (Singleton) - Amazon OpenSearch
        services.AddSingleton<IVectorSearch, OpenSearchVectorSearch>();

        // 12. Register incremental indexing service (Scoped)
        services.AddScoped<IIncrementalIndexingService, IncrementalIndexingService>();

        // 13. Register pipeline orchestrator (Scoped)
        services.AddScoped<IPipelineOrchestrator, PipelineOrchestrator>();

        return services;
    }
}
