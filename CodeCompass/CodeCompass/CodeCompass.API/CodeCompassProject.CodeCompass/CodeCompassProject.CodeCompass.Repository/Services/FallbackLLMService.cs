using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CodeCompass.Core.Configuration;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// LLM service that tries Amazon Bedrock first, then falls back to local context extraction.
/// Returns a warning message to the user when Bedrock is unavailable.
/// </summary>
public class FallbackLLMService : ILLMService
{
    private readonly AwsBedrockSettings _settings;
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly ILogger<FallbackLLMService> _logger;
    private bool _bedrockAvailable = true;

    public FallbackLLMService(
        IOptions<AwsBedrockSettings> settings,
        ILogger<FallbackLLMService> logger)
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

        // Try Bedrock first (if not already known to be down)
        if (_bedrockAvailable)
        {
            try
            {
                var answer = await CallBedrockAsync(systemPrompt, userMessage, contextList, cancellationToken);
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    return new ChatCompletionResult
                    {
                        Content = answer,
                        PromptTokens = userMessage.Length / 4,
                        CompletionTokens = answer.Length / 4
                    };
                }
            }
            catch (Exception ex)
            {
                _bedrockAvailable = false;
                _logger.LogWarning(ex, "[FALLBACK-LLM] Bedrock unavailable: {Message}. Switching to local mode.", ex.Message);
            }
        }

        // Fallback: local context extraction with warning
        var localAnswer = GenerateLocalAnswer(userMessage, contextList);
        return new ChatCompletionResult
        {
            Content = localAnswer,
            PromptTokens = userMessage.Length / 4,
            CompletionTokens = localAnswer.Length / 4
        };
    }

    private async Task<string> CallBedrockAsync(
        string systemPrompt, string userMessage, List<string> contextList, CancellationToken cancellationToken)
    {
        var contextBlock = contextList.Count == 0
            ? "No relevant context was retrieved."
            : string.Join("\n\n---\n\n", contextList);

        var fullPrompt = $"{systemPrompt}\n\nContext:\n{contextBlock}\n\nQuestion: {userMessage}";

        var requestBody = JsonSerializer.Serialize(new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 1024,
            temperature = 0.2,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[] { new { type = "text", text = fullPrompt } }
                }
            }
        });

        var request = new InvokeModelRequest
        {
            ModelId = _settings.ChatModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestBody))
        };

        var response = await _client.InvokeModelAsync(request, cancellationToken);
        using var responseStream = response.Body;
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            var first = contentArray[0];
            if (first.TryGetProperty("text", out var textProperty))
            {
                return textProperty.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private string GenerateLocalAnswer(string userMessage, List<string> contextList)
    {
        if (contextList.Count == 0)
        {
            return "⚠️ **Note:** Amazon Bedrock LLM is currently unavailable (Access Denied by organization policy). " +
                   "No relevant context was found for your question.";
        }

        // Extract relevant sentences using keyword matching
        var stopWords = new HashSet<string> { "what", "is", "a", "an", "the", "how", "does", "do", "can", "which", "where", "when", "why", "are", "was", "were", "have", "has", "had", "will", "would", "could", "should", "this", "that", "in", "on", "at", "to", "for", "of", "with", "by", "from", "about", "and", "or", "but", "not", "it", "its" };
        var queryWords = userMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('?', '.', '!', ',').ToLowerInvariant())
            .Where(w => w.Length > 1 && !stopWords.Contains(w))
            .ToHashSet();

        var scoredLines = new List<(string Line, int Score)>();

        foreach (var chunk in contextList)
        {
            var text = chunk;
            if (text.StartsWith("[Source:"))
            {
                var end = text.IndexOf(']');
                if (end > 0) text = text[(end + 1)..].Trim();
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 20 && !l.StartsWith("---") && !l.StartsWith("```"));

            foreach (var line in lines)
            {
                var lower = line.ToLowerInvariant();
                var score = queryWords.Count(w => lower.Contains(w));
                if (score > 0) scoredLines.Add((line, score));
            }
        }

        var bestLines = scoredLines
            .OrderByDescending(s => s.Score)
            .Select(s => s.Line)
            .Distinct()
            .Take(6)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("⚠️ **Note:** Amazon Bedrock LLM is currently unavailable (Access Denied by organization policy). " +
                     "Showing relevant excerpts from the knowledge base instead.\n");
        sb.AppendLine("---\n");

        if (bestLines.Count > 0)
        {
            foreach (var line in bestLines)
            {
                sb.AppendLine($"• {line}");
            }
        }
        else
        {
            // Just show first chunk content
            var first = contextList[0];
            if (first.StartsWith("[Source:"))
            {
                var end = first.IndexOf(']');
                if (end > 0) first = first[(end + 1)..].Trim();
            }
            sb.AppendLine(first.Length > 500 ? first[..500] + "..." : first);
        }

        return sb.ToString().Trim();
    }
}
