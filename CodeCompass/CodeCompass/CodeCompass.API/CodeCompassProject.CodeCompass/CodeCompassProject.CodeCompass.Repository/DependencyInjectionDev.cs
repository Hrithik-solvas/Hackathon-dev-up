using CodeCompass.Core.Interfaces;
using CodeCompass.Pipeline;
using CodeCompassProject.CodeCompass.Application.Commands;
using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using CodeCompassProject.CodeCompass.Application.Queries;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using CodeCompassProject.CodeCompass.Repository.Services;
using CodeCompassProject.CodeCompass.Repository.Services.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCompassProject.CodeCompass.Repository;

/// <summary>
/// Development-mode DI registration that uses fake/stub implementations.
/// No Azure services required — the API runs locally with mock data.
/// </summary>
public static class DependencyInjectionDev
{
    public static IServiceCollection AddDevelopmentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Use the real RAG pipeline in Development so chat and ingestion exercise the AWS/OpenSearch stack.
        services.AddCodeCompassPipeline(configuration);

        // Configuration
        services.Configure<KnowledgeBasesSettings>(
            configuration.GetSection(KnowledgeBasesSettings.SectionName));

        // Use the real Bedrock-backed LLM so Development exercises the same chat path.
        services.AddScoped<ILLMService, BedrockLLMService>();
        services.AddScoped<IEmbeddingService, EmbeddingServiceAdapter>();
        services.AddScoped<CodeCompassProject.CodeCompass.Application.Interfaces.IVectorStore, RagVectorStoreAdapter>();

        // Question routing (real implementation - no Azure dependency)
        services.AddSingleton<IQuestionRouter, KeywordQuestionRouter>();

        // Document ingestion (real implementation)
        services.AddScoped<IDocumentIngestionService, DefaultDocumentIngestionService>();

        // CQRS Handlers (real implementations)
        services.AddScoped<ICommandHandler<SendChatMessageCommand, ChatResponse>, SendChatMessageHandler>();
        services.AddScoped<ICommandHandler<IngestDocumentsCommand, IngestResponse>, IngestDocumentsHandler>();
        services.AddScoped<ICommandHandler<IngestCodeCommand, IngestResponse>, IngestCodeHandler>();
        services.AddScoped<IQueryHandler<GetHealthQuery, HealthResponse>, GetHealthHandler>();

        // NO ConfigurationValidationHostedService — development should not fail startup on missing secrets.

        return services;
    }
}
