using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Local LLM service that synthesizes answers from retrieved context chunks.
/// No external API calls needed - formats the relevant context into a coherent answer.
/// </summary>
public class LocalLLMService : ILLMService
{
    private readonly ILogger<LocalLLMService> _logger;

    public LocalLLMService(ILogger<LocalLLMService> logger)
    {
        _logger = logger;
    }

    public Task<ChatCompletionResult> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        IEnumerable<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        var chunks = contextChunks.ToList();

        string answer;
        if (chunks.Count == 0)
        {
            answer = "I could not find relevant context for that question in the indexed documentation.";
        }
        else
        {
            // Extract the most relevant sentences from chunks that contain query keywords
            var queryWords = userMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .Select(w => w.TrimEnd('?', '.', '!').ToLowerInvariant())
                .ToHashSet();

            var relevantSentences = new List<string>();

            foreach (var chunk in chunks)
            {
                // Remove the [Source: ...] prefix
                var text = chunk;
                var sourceEnd = text.IndexOf(']');
                if (text.StartsWith("[Source:") && sourceEnd > 0)
                    text = text[(sourceEnd + 1)..].Trim();

                // Split into sentences and find ones matching query keywords
                var sentences = text.Split(new[] { ". ", ".\n", ".\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 20)
                    .ToList();

                foreach (var sentence in sentences)
                {
                    var lower = sentence.ToLowerInvariant();
                    if (queryWords.Any(w => lower.Contains(w)))
                    {
                        var cleaned = sentence.TrimEnd('.') + ".";
                        if (!relevantSentences.Contains(cleaned) && relevantSentences.Count < 5)
                            relevantSentences.Add(cleaned);
                    }
                }
            }

            if (relevantSentences.Count > 0)
            {
                answer = string.Join("\n\n", relevantSentences);
            }
            else
            {
                // Fallback: use first 300 chars of first chunk
                var firstChunk = chunks[0];
                var sourceEnd = firstChunk.IndexOf(']');
                if (firstChunk.StartsWith("[Source:") && sourceEnd > 0)
                    firstChunk = firstChunk[(sourceEnd + 1)..].Trim();

                answer = firstChunk.Length > 500 ? firstChunk[..500] + "..." : firstChunk;
            }
        }

        _logger.LogInformation("[LOCAL-LLM] Generated answer from {ChunkCount} context chunks", chunks.Count);

        return Task.FromResult(new ChatCompletionResult
        {
            Content = answer,
            PromptTokens = userMessage.Length / 4,
            CompletionTokens = answer.Length / 4
        });
    }
}
