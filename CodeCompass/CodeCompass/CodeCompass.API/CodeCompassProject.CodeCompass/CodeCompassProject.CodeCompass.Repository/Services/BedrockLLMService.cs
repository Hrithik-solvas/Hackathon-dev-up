using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CodeCompass.Core.Configuration;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Bedrock-backed LLM service for chat completions.
/// Uses Anthropic Claude by default, with Titan text as the fallback request format.
/// </summary>
public class BedrockLLMService : ILLMService
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly AwsBedrockSettings _settings;
    private readonly ILogger<BedrockLLMService> _logger;

    public BedrockLLMService(
        IOptions<AwsBedrockSettings> settings,
        ILogger<BedrockLLMService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new AmazonBedrockRuntimeClient(Amazon.RegionEndpoint.GetBySystemName(_settings.Region));
    }

    public async Task<ChatCompletionResult> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        IEnumerable<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        var contextList = contextChunks.ToList();
        var prompt = BuildPrompt(systemPrompt, userMessage, contextList);

        try
        {
            var content = await InvokeModelAsync(prompt, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = contextList.Any()
                    ? string.Join("\n\n", contextList.Take(2))
                    : "I could not find relevant context for that question.";
            }

            return new ChatCompletionResult
            {
                Content = content,
                PromptTokens = prompt.Length / 4,
                CompletionTokens = content.Length / 4
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bedrock chat completion failed.");

            var fallback = contextList.Any()
                ? string.Join("\n\n", contextList.Take(2))
                : "I could not find relevant context for that question.";

            return new ChatCompletionResult
            {
                Content = fallback,
                PromptTokens = prompt.Length / 4,
                CompletionTokens = fallback.Length / 4
            };
        }
    }

    private async Task<string> InvokeModelAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new InvokeModelRequest
        {
            ModelId = _settings.ChatModelId,
            ContentType = "application/json",
            Accept = "application/json"
        };

        request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BuildRequestBody(prompt)));

        var response = await _client.InvokeModelAsync(request, cancellationToken).ConfigureAwait(false);
        using var responseStream = response.Body;
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            var first = contentArray[0];
            if (first.TryGetProperty("text", out var textProperty))
            {
                return textProperty.GetString() ?? string.Empty;
            }
        }

        if (document.RootElement.TryGetProperty("results", out var resultsArray) && resultsArray.GetArrayLength() > 0)
        {
            var first = resultsArray[0];
            if (first.TryGetProperty("outputText", out var outputText))
            {
                return outputText.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private string BuildRequestBody(string prompt)
    {
        if (_settings.ChatModelId.Contains("claude", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 512,
                temperature = 0.2,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "text", text = prompt }
                        }
                    }
                }
            });
        }

        return JsonSerializer.Serialize(new
        {
            inputText = prompt,
            textGenerationConfig = new
            {
                maxTokenCount = 512,
                temperature = 0.2,
                topP = 0.9
            }
        });
    }

    private static string BuildPrompt(string systemPrompt, string userMessage, IReadOnlyCollection<string> contextChunks)
    {
        var contextBlock = contextChunks.Count == 0
            ? "No relevant context was retrieved."
            : string.Join("\n\n---\n\n", contextChunks);

        return $"""
{systemPrompt}

Context:
{contextBlock}

Question: {userMessage}
""";
    }
}