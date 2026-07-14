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
            // Clean chunks - remove [Source: ...] prefix
            var cleanedChunks = chunks.Select(chunk =>
            {
                var text = chunk;
                if (text.StartsWith("[Source:"))
                {
                    var endBracket = text.IndexOf(']');
                    if (endBracket > 0) text = text[(endBracket + 1)..].Trim();
                }
                return text;
            }).ToList();

            // Extract query keywords (ignore common words)
            var stopWords = new HashSet<string> { "what", "is", "a", "an", "the", "how", "does", "do", "can", "which", "where", "when", "why", "are", "was", "were", "been", "being", "have", "has", "had", "will", "would", "could", "should", "may", "might", "shall", "this", "that", "these", "those", "in", "on", "at", "to", "for", "of", "with", "by", "from", "about", "between", "through", "during", "before", "after", "above", "below", "and", "or", "but", "not", "no", "if", "then", "than", "so", "it", "its" };
            var queryWords = userMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('?', '.', '!', ',').ToLowerInvariant())
                .Where(w => w.Length > 1 && !stopWords.Contains(w))
                .ToHashSet();

            // Score each sentence by how many query keywords it contains
            var scoredSentences = new List<(string Sentence, int Score)>();

            foreach (var chunk in cleanedChunks)
            {
                // Split into sentences (handle markdown tables, bullet points, etc.)
                var lines = chunk.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 15 && !l.StartsWith("---") && !l.StartsWith("```"))
                    .ToList();

                foreach (var line in lines)
                {
                    var lower = line.ToLowerInvariant();
                    var score = queryWords.Count(w => lower.Contains(w));
                    if (score > 0)
                    {
                        scoredSentences.Add((line, score));
                    }
                }
            }

            // Take the top-scoring unique sentences
            var bestSentences = scoredSentences
                .OrderByDescending(s => s.Score)
                .Select(s => s.Sentence)
                .Distinct()
                .Take(8)
                .ToList();

            if (bestSentences.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Based on the documentation, here's what I found about your question:\n");
                foreach (var sentence in bestSentences)
                {
                    sb.AppendLine($"• {sentence}");
                }
                answer = sb.ToString().Trim();
            }
            else
            {
                // Fallback: use first chunk content directly
                var firstChunk = cleanedChunks[0];
                answer = firstChunk.Length > 600 ? firstChunk[..600] + "..." : firstChunk;
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
