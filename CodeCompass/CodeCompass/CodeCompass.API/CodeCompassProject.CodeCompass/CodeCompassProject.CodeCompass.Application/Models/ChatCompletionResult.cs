namespace CodeCompassProject.CodeCompass.Application.Models;

public class ChatCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}
