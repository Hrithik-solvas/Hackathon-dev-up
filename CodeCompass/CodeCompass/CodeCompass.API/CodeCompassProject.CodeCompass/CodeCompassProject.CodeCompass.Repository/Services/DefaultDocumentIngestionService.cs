using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Domain.Entities;
using CodeCompassProject.CodeCompass.Repository.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Default document ingestion service that chunks text by size with overlap.
/// </summary>
public class DefaultDocumentIngestionService : IDocumentIngestionService
{
    private readonly IngestionSettings _settings;
    private readonly ILogger<DefaultDocumentIngestionService> _logger;

    public DefaultDocumentIngestionService(
        IOptions<IngestionSettings> settings,
        ILogger<DefaultDocumentIngestionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<DocumentChunk>> ChunkDocumentAsync(
        Stream content, string sourceUri, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        _logger.LogDebug("Chunking document {SourceUri} with {Length} characters", sourceUri, text.Length);

        var chunks = ChunkText(text, sourceUri, SourceType.Documentation);
        return chunks;
    }

    public async Task<IEnumerable<DocumentChunk>> ChunkCodeAsync(
        Stream content, string sourceUri, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        _logger.LogDebug("Chunking code file {SourceUri} with {Length} characters", sourceUri, text.Length);

        // For code, try to split on logical boundaries (methods, classes)
        var chunks = ChunkCode(text, sourceUri);
        return chunks;
    }

    private List<DocumentChunk> ChunkText(string text, string sourceUri, SourceType sourceType)
    {
        var chunks = new List<DocumentChunk>();
        var maxChunkSize = _settings.MaxChunkSize;
        var overlap = _settings.ChunkOverlap;

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var position = 0;
        var chunkIndex = 0;

        while (position < text.Length)
        {
            var length = Math.Min(maxChunkSize, text.Length - position);
            var chunkText = text.Substring(position, length);

            // Try to break at a sentence or paragraph boundary
            if (position + length < text.Length)
            {
                var lastBreak = chunkText.LastIndexOfAny(new[] { '.', '\n', '!', '?' });
                if (lastBreak > maxChunkSize / 2)
                {
                    chunkText = chunkText[..(lastBreak + 1)];
                    length = lastBreak + 1;
                }
            }

            chunks.Add(new DocumentChunk
            {
                Content = chunkText.Trim(),
                SourceUri = sourceUri,
                SourceType = sourceType,
                Metadata = new Dictionary<string, string>
                {
                    ["chunkIndex"] = chunkIndex.ToString(),
                    ["charOffset"] = position.ToString()
                }
            });

            position += length - overlap;
            chunkIndex++;

            if (position >= text.Length) break;
        }

        _logger.LogDebug("Created {ChunkCount} chunks from document {SourceUri}", chunks.Count, sourceUri);
        return chunks;
    }

    private List<DocumentChunk> ChunkCode(string text, string sourceUri)
    {
        var chunks = new List<DocumentChunk>();
        var maxChunkSize = _settings.MaxChunkSize;
        var overlap = _settings.ChunkOverlap;

        // Split code by double newlines (blank lines) which often separate logical blocks
        var blocks = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = string.Empty;
        var chunkIndex = 0;

        foreach (var block in blocks)
        {
            if (currentChunk.Length + block.Length > maxChunkSize && !string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(new DocumentChunk
                {
                    Content = currentChunk.Trim(),
                    SourceUri = sourceUri,
                    SourceType = SourceType.Code,
                    Metadata = new Dictionary<string, string>
                    {
                        ["chunkIndex"] = chunkIndex.ToString(),
                        ["language"] = DetectLanguage(sourceUri)
                    }
                });
                chunkIndex++;

                // Keep overlap from the end of the current chunk
                currentChunk = currentChunk.Length > overlap
                    ? currentChunk[^overlap..] + "\n\n" + block
                    : block;
            }
            else
            {
                currentChunk = string.IsNullOrWhiteSpace(currentChunk)
                    ? block
                    : currentChunk + "\n\n" + block;
            }
        }

        // Add remaining content
        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(new DocumentChunk
            {
                Content = currentChunk.Trim(),
                SourceUri = sourceUri,
                SourceType = SourceType.Code,
                Metadata = new Dictionary<string, string>
                {
                    ["chunkIndex"] = chunkIndex.ToString(),
                    ["language"] = DetectLanguage(sourceUri)
                }
            });
        }

        _logger.LogDebug("Created {ChunkCount} chunks from code file {SourceUri}", chunks.Count, sourceUri);
        return chunks;
    }

    private static string DetectLanguage(string sourceUri)
    {
        var extension = Path.GetExtension(sourceUri)?.ToLowerInvariant();
        return extension switch
        {
            ".cs" => "csharp",
            ".ts" or ".tsx" => "typescript",
            ".js" or ".jsx" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rs" => "rust",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".md" => "markdown",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            _ => "unknown"
        };
    }
}
