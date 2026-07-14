using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;

namespace CodeCompassProject.CodeCompass.Repository.Services.Fakes;

/// <summary>
/// Fake LLM service for development mode.
/// Generates realistic-looking responses based on the provided context without calling Azure OpenAI.
/// </summary>
public class FakeLLMService : ILLMService
{
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
            answer = "I don't have enough information in the knowledge base to answer this question. " +
                     "Please try rephrasing your question or ensure the relevant documents have been ingested.";
        }
        else
        {
            // Build a fake but realistic-looking answer from context
            answer = GenerateAnswer(userMessage, chunks);
        }

        var result = new ChatCompletionResult
        {
            Content = answer,
            PromptTokens = 150 + (chunks.Count * 100),
            CompletionTokens = answer.Split(' ').Length
        };

        return Task.FromResult(result);
    }

    private static string GenerateAnswer(string question, List<string> contextChunks)
    {
        // Extract source annotations from context
        var productSources = contextChunks.Where(c => c.Contains("[Source: Product_Knowledge_Base]")).ToList();
        var techSources = contextChunks.Where(c => c.Contains("[Source: Tech_Stack_Knowledge_Base]")).ToList();

        var parts = new List<string>();

        parts.Add($"Based on the available documentation, here's what I found regarding your question:\n");

        if (productSources.Any())
        {
            var firstChunk = productSources.First()
                .Replace("[Source: Product_Knowledge_Base] ", "");
            var summary = firstChunk.Length > 200 ? firstChunk[..200] + "..." : firstChunk;
            parts.Add($"**From Product Knowledge Base:**\n{summary}\n");
        }

        if (techSources.Any())
        {
            var firstChunk = techSources.First()
                .Replace("[Source: Tech_Stack_Knowledge_Base] ", "");
            var summary = firstChunk.Length > 200 ? firstChunk[..200] + "..." : firstChunk;
            parts.Add($"**From Tech Stack Knowledge Base:**\n{summary}\n");
        }

        if (!productSources.Any() && !techSources.Any() && contextChunks.Any())
        {
            var firstChunk = contextChunks.First();
            var cleanChunk = System.Text.RegularExpressions.Regex.Replace(firstChunk, @"\[Source: .*?\] ", "");
            var summary = cleanChunk.Length > 300 ? cleanChunk[..300] + "..." : cleanChunk;
            parts.Add(summary);
        }

        parts.Add($"\n*This answer is grounded in {contextChunks.Count} source document(s) from the knowledge base.*");

        return string.Join("\n", parts);
    }
}
