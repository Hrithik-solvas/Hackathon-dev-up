using System.Net.Http.Json;
using System.Text.Json;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// LLM service using Google Gemini API (free tier, 15 req/min).
/// </summary>
public class GeminiLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiLLMService> _logger;

    public GeminiLLMService(
        IOptions<GeminiSettings> settings,
        ILogger<GeminiLLMService> logger)
    {
        _httpClient = new HttpClient();
        _apiKey = settings.Value.ApiKey;
        _model = settings.Value.Model;
        _logger = logger;
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

        var fullPrompt = $"{systemPrompt}\n\nContext:\n{contextBlock}\n\nQuestion: {userMessage}\n\nProvide a clear, concise answer based only on the context above.";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024
            }
        };

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            _logger.LogDebug("[GEMINI] Calling model={Model}", _model);

            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[GEMINI] API returned {StatusCode}: {Error}", response.StatusCode, error);

                // Fallback to context extraction
                return FallbackResponse(contextList, userMessage);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var content = ExtractContent(json);

            if (string.IsNullOrWhiteSpace(content))
            {
                return FallbackResponse(contextList, userMessage);
            }

            return new ChatCompletionResult
            {
                Content = content,
                PromptTokens = fullPrompt.Length / 4,
                CompletionTokens = content.Length / 4
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GEMINI] Error: {Message}", ex.Message);
            return FallbackResponse(contextList, userMessage);
        }
    }

    private static string ExtractContent(JsonDocument? json)
    {
        if (json == null) return string.Empty;

        try
        {
            if (json.RootElement.TryGetProperty("candidates", out var candidates)
                && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    private ChatCompletionResult FallbackResponse(List<string> contextList, string userMessage)
    {
        var fallback = contextList.Count > 0
            ? $"Based on the documentation: {contextList[0]}"
            : "I could not find relevant context for that question.";

        if (fallback.Length > 500) fallback = fallback[..500] + "...";

        return new ChatCompletionResult
        {
            Content = fallback,
            PromptTokens = userMessage.Length / 4,
            CompletionTokens = fallback.Length / 4
        };
    }
}
