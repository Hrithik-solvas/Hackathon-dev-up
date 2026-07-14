using CodeCompassProject.CodeCompass.Application.Models;

namespace CodeCompassProject.CodeCompass.Application.Interfaces;

public interface ILLMService
{
    Task<ChatCompletionResult> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        IEnumerable<string> contextChunks,
        CancellationToken cancellationToken = default);
}
