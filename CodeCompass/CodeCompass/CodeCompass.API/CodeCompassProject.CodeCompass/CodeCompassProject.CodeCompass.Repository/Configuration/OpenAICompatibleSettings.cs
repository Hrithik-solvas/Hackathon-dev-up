namespace CodeCompassProject.CodeCompass.Repository.Configuration;

/// <summary>
/// Settings for OpenAI-compatible API endpoints (LiteLLM, OpenAI, Ollama, etc.)
/// </summary>
public class OpenAICompatibleSettings
{
    public const string SectionName = "OpenAICompatible";

    /// <summary>
    /// Base URL of the API (e.g., "http://localhost:4000" for LiteLLM, "https://api.openai.com" for OpenAI)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// API key (leave empty for Ollama or unauthenticated endpoints)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Embedding model name (e.g., "text-embedding-3-small", "nomic-embed-text", "bedrock/titan-embed-text-v2")
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Chat model name (e.g., "gpt-4o-mini", "llama3.2", "bedrock/claude-3-haiku")
    /// </summary>
    public string ChatModel { get; set; } = "llama3.2";

    /// <summary>
    /// Embedding vector dimension (must match what the model produces)
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;
}
