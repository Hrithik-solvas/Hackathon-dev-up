using System.Net.Http.Json;
using System.Text.Json;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// LLM service that calls any OpenAI-compatible /v1/chat/completions endpoint.
/// Works with: OpenAI, LiteLLM proxy, Ollama, Azure OpenAI, etc.
/// </summary>
public class OpenAICompatibleLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAICompatibleSettings _settings;
    private readonly ILogger<OpenAICompatibleLLMService> _logger;

    public OpenAICompatibleLLMService(
        HttpClient httpClient,
        IOptions<OpenAICompatibleSettings> settings,
        ILogger<OpenAICompatibleLLMService> logger)
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

    public async Task<ChatCompletionResult> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        IEnumerable<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        var contextList = contextChunks.ToList();
        var contextBlock = contextList.Count == 0
            ? "No relevant context was retrieved."
            : string.Join("\n\n---\n\n", contextList);

        var fullSystemPrompt = $"{systemPrompt}\n\nContext:\n{contextBlock}";

        var requestBody = new
        {
            model = _settings.ChatModel,
            messages = new object[]
            {
                new { role = "system", content = fullSystemPrompt },
                new { role = "user", content = userMessage }
            },
            max_tokens = 1024,
            temperature = 0.2
        };

        try
        {
            _logger.LogDebug("[LLM] Calling {BaseUrl} with model={Model}",
                _settings.BaseUrl, _settings.ChatModel);

            var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[LLM] API returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                var fallback = contextList.Count > 0
                    ? string.Join("\n\n", contextList.Take(2))
                    : "I could not find relevant context for that question.";

                return new ChatCompletionResult
                {
                    Content = fallback,
                    PromptTokens = fullSystemPrompt.Length / 4,
                    CompletionTokens = fallback.Length / 4
                };
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var content = string.Empty;
            int promptTokens = 0, completionTokens = 0;

            if (json?.RootElement.TryGetProperty("choices", out var choices) == true
                && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var contentProp))
                {
                    content = contentProp.GetString() ?? string.Empty;
                }
            }

            if (json?.RootElement.TryGetProperty("usage", out var usage) == true)
            {
                usage.TryGetProperty("prompt_tokens", out var pt);
                usage.TryGetProperty("completion_tokens", out var ct);
                promptTokens = pt.ValueKind == JsonValueKind.Number ? pt.GetInt32() : 0;
                completionTokens = ct.ValueKind == JsonValueKind.Number ? ct.GetInt32() : 0;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = contextList.Count > 0
                    ? string.Join("\n\n", contextList.Take(2))
                    : "I could not find relevant context for that question.";
            }

            return new ChatCompletionResult
            {
                Content = content,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LLM] Error calling chat endpoint: {Message}", ex.Message);

            var fallback = contextList.Count > 0
                ? string.Join("\n\n", contextList.Take(2))
                : "I could not find relevant context for that question.";

            return new ChatCompletionResult
            {
                Content = fallback,
                PromptTokens = 0,
                CompletionTokens = 0
            };
        }
    }
}
