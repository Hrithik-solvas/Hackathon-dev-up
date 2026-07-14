using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Application.Commands;

public class IngestCodeHandler : ICommandHandler<IngestCodeCommand, IngestResponse>
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<IngestCodeHandler> _logger;

    public IngestCodeHandler(
        IDocumentIngestionService ingestionService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<IngestCodeHandler> logger)
    {
        _ingestionService = ingestionService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IngestResponse> HandleAsync(IngestCodeCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ingesting {FileCount} code file(s) from repository {Repo}",
            command.Files.Count, command.RepositoryName ?? "unknown");

        var totalChunks = 0;

        foreach (var file in command.Files)
        {
            await using var stream = file.OpenReadStream();
            var sourceUri = string.IsNullOrEmpty(command.RepositoryName)
                ? file.FileName
                : $"{command.RepositoryName}/{file.FileName}";

            // 1. Chunk the code file
            var chunks = (await _ingestionService.ChunkCodeAsync(stream, sourceUri, cancellationToken)).ToList();

            // 2. Generate embeddings
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = (await _embeddingService.GetEmbeddingsAsync(contents, cancellationToken)).ToList();

            for (var i = 0; i < chunks.Count; i++)
            {
                chunks[i].EmbeddingVector = embeddings[i];
                chunks[i].SourceType = SourceType.Code;
                chunks[i].Metadata["repository"] = command.RepositoryName ?? "unknown";
                chunks[i].Metadata["filename"] = file.FileName;
            }

            // 3. Store in vector store
            await _vectorStore.StoreAsync(chunks, cancellationToken);
            totalChunks += chunks.Count;

            _logger.LogInformation("Ingested {ChunkCount} chunks from code file {FileName}", chunks.Count, file.FileName);
        }

        return new IngestResponse
        {
            ChunksIngested = totalChunks,
            SourcesProcessed = command.Files.Count
        };
    }
}
