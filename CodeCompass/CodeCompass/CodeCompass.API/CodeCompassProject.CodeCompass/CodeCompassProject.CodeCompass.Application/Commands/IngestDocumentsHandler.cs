using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Application.Commands;

public class IngestDocumentsHandler : ICommandHandler<IngestDocumentsCommand, IngestResponse>
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<IngestDocumentsHandler> _logger;

    public IngestDocumentsHandler(
        IDocumentIngestionService ingestionService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<IngestDocumentsHandler> logger)
    {
        _ingestionService = ingestionService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IngestResponse> HandleAsync(IngestDocumentsCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ingesting {FileCount} document(s)", command.Files.Count);

        var totalChunks = 0;

        foreach (var file in command.Files)
        {
            await using var stream = file.OpenReadStream();
            var sourceUri = file.FileName;

            // 1. Chunk the document
            var chunks = (await _ingestionService.ChunkDocumentAsync(stream, sourceUri, cancellationToken)).ToList();

            // 2. Generate embeddings for each chunk
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = (await _embeddingService.GetEmbeddingsAsync(contents, cancellationToken)).ToList();

            for (var i = 0; i < chunks.Count; i++)
            {
                chunks[i].EmbeddingVector = embeddings[i];
                chunks[i].SourceType = SourceType.Documentation;

                if (command.Metadata != null)
                {
                    foreach (var kvp in command.Metadata)
                    {
                        chunks[i].Metadata[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 3. Store in vector store
            await _vectorStore.StoreAsync(chunks, cancellationToken);
            totalChunks += chunks.Count;

            _logger.LogInformation("Ingested {ChunkCount} chunks from {FileName}", chunks.Count, file.FileName);
        }

        return new IngestResponse
        {
            ChunksIngested = totalChunks,
            SourcesProcessed = command.Files.Count
        };
    }
}
