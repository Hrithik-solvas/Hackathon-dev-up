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

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register RAG pipeline (parsers, embedding, search, indexing)
        services.AddCodeCompassPipeline(configuration);

        // Configuration
        services.Configure<KnowledgeBasesSettings>(
            configuration.GetSection(KnowledgeBasesSettings.SectionName));

        // Adapter: IEmbeddingService delegates to IEmbeddingGenerator (registered by RAG)
        services.AddScoped<IEmbeddingService, EmbeddingServiceAdapter>();

        // Bridge the legacy application vector-store interface to the real RAG vector store.
        services.AddScoped<CodeCompassProject.CodeCompass.Application.Interfaces.IVectorStore, RagVectorStoreAdapter>();

        // Question routing
        services.AddSingleton<IQuestionRouter, KeywordQuestionRouter>();

        // LLM backed by AWS Bedrock.
        services.AddScoped<ILLMService, BedrockLLMService>();

        // Document ingestion
        services.AddScoped<IDocumentIngestionService, DefaultDocumentIngestionService>();

        // CQRS Handlers
        services.AddScoped<ICommandHandler<SendChatMessageCommand, ChatResponse>, SendChatMessageHandler>();
        services.AddScoped<ICommandHandler<IngestDocumentsCommand, IngestResponse>, IngestDocumentsHandler>();
        services.AddScoped<ICommandHandler<IngestCodeCommand, IngestResponse>, IngestCodeHandler>();
        services.AddScoped<IQueryHandler<GetHealthQuery, HealthResponse>, GetHealthHandler>();

        // Startup validation
        services.AddHostedService<ConfigurationValidationHostedService>();

        return services;
    }
}
