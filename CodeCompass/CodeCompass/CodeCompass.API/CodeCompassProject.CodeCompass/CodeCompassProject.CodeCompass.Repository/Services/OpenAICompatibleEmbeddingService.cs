using System.Net.Http.Json;
using System.Text.Json;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Embedding service that calls any OpenAI-compatible /v1/embeddings endpoint.
/// Works with: OpenAI, LiteLLM proxy, Ollama, Azure OpenAI, etc.
/// </summary>
public class OpenAICompatibleEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAICompatibleSettings _settings;
    private readonly ILogger<OpenAICompatibleEmbeddingService> _logger;

    public OpenAICompatibleEmbeddingService(
        HttpClient httpClient,
        IOptions<OpenAICompatibleSettings> settings,
        ILogger<OpenAICompatibleEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await CallEmbeddingEndpoint(new[] { text }, cancellationToken);
        return result.Count > 0 ? result[0] : Array.Empty<float>();
    }

    public async Task<IEnumerable<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0) return Array.Empty<float[]>();

        // Process in batches of 16
        var allEmbeddings = new List<float[]>();
        const int batchSize = 16;

        for (int i = 0; i < textList.Count; i += batchSize)
        {
            var batch = textList.Skip(i).Take(batchSize).ToArray();
            var batchResult = await CallEmbeddingEndpoint(batch, cancellationToken);
            allEmbeddings.AddRange(batchResult);

            _logger.LogDebug("[EMBED] Processed batch {Start}-{End}/{Total}",
                i + 1, Math.Min(i + batchSize, textList.Count), textList.Count);
        }

        return allEmbeddings;
    }

    private async Task<List<float[]>> CallEmbeddingEndpoint(string[] inputs, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _settings.EmbeddingModel,
            input = inputs
        };

        try
        {
            _logger.LogDebug("[EMBED] Calling {BaseUrl} with model={Model}, inputs={Count}",
                _settings.BaseUrl, _settings.EmbeddingModel, inputs.Length);

            var response = await _httpClient.PostAsJsonAsync("/v1/embeddings", requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[EMBED] API returned {StatusCode}: {Error}",
                    response.StatusCode, errorBody);
                return inputs.Select(_ => Array.Empty<float>()).ToList();
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var embeddings = new List<float[]>();

            if (json?.RootElement.TryGetProperty("data", out var dataArray) == true)
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("embedding", out var embeddingArray))
                    {
                        var vector = new float[embeddingArray.GetArrayLength()];
                        int idx = 0;
                        foreach (var val in embeddingArray.EnumerateArray())
                        {
                            vector[idx++] = val.GetSingle();
                        }
                        embeddings.Add(vector);
                    }
                }
            }

            _logger.LogDebug("[EMBED] Got {Count} embeddings, dimension={Dim}",
                embeddings.Count, embeddings.FirstOrDefault()?.Length ?? 0);

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMBED] Error calling embedding endpoint: {Message}", ex.Message);
            return inputs.Select(_ => Array.Empty<float>()).ToList();
        }
    }
}
