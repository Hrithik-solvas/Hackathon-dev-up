using System.Diagnostics;
using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Application.Queries;

public class GetHealthHandler : IQueryHandler<GetHealthQuery, HealthResponse>
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILLMService _llmService;
    private readonly ILogger<GetHealthHandler> _logger;

    public GetHealthHandler(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ILLMService llmService,
        ILogger<GetHealthHandler> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<HealthResponse> HandleAsync(GetHealthQuery query, CancellationToken cancellationToken = default)
    {
        var services = new List<ServiceHealth>();

        // Check Vector Store
        services.Add(await CheckServiceAsync("VectorStore", async () =>
        {
            await _vectorStore.SearchAsync(new float[] { 0.0f }, 1, cancellationToken);
        }));

        // Check Embedding Service
        services.Add(await CheckServiceAsync("EmbeddingService", async () =>
        {
            await _embeddingService.GetEmbeddingAsync("health check", cancellationToken);
        }));

        // Check LLM Service
        services.Add(await CheckServiceAsync("LLMService", async () =>
        {
            await _llmService.GetCompletionAsync("You are a health check.", "ping", Enumerable.Empty<string>(), cancellationToken);
        }));

        var overallStatus = services.All(s => s.Status == "Healthy") ? "Healthy" :
                           services.Any(s => s.Status == "Healthy") ? "Degraded" : "Unhealthy";

        return new HealthResponse
        {
            Status = overallStatus,
            Services = services
        };
    }

    private async Task<ServiceHealth> CheckServiceAsync(string serviceName, Func<Task> healthCheck)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await healthCheck();
            sw.Stop();
            return new ServiceHealth
            {
                Name = serviceName,
                Status = "Healthy",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health check failed for {ServiceName}", serviceName);
            return new ServiceHealth
            {
                Name = serviceName,
                Status = "Unhealthy",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }
}
