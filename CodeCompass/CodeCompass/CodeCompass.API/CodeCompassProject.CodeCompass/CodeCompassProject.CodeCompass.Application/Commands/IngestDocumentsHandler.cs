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
            _logger.LogInformation("[INGEST] Starting chunking for {FileName}", sourceUri);
            var chunks = (await _ingestionService.ChunkDocumentAsync(stream, sourceUri, cancellationToken)).ToList();
            _logger.LogInformation("[INGEST] Chunking complete: {ChunkCount} chunks created", chunks.Count);

            // 2. Generate embeddings for each chunk
            var contents = chunks.Select(c => c.Content).ToList();
            _logger.LogInformation("[INGEST] Starting embedding generation for {Count} chunks...", contents.Count);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var embeddings = new List<float[]>();
            for (var i = 0; i < contents.Count; i++)
            {
                try
                {
                    _logger.LogDebug("[INGEST] Calling Bedrock for chunk {Index}...", i);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(15)); // 15s timeout per embedding
                    
                    var embedding = await _embeddingService.GetEmbeddingAsync(contents[i], cts.Token);
                    
                    // Fail fast if first embedding returns empty (likely auth error)
                    if (i == 0 && (embedding == null || embedding.Length == 0))
                    {
                        _logger.LogError("[INGEST] FATAL: First embedding returned empty vector. AWS credentials are likely invalid. Aborting ingestion.");
                        throw new InvalidOperationException(
                            "Embedding generation failed - AWS Bedrock returned empty vector. " +
                            "Please check your AWS credentials (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, AWS_SESSION_TOKEN) are valid and not expired.");
                    }
                    
                    embeddings.Add(embedding);
                    if ((i + 1) % 5 == 0 || i == 0)
                    {
                        _logger.LogInformation("[INGEST] Embedded chunk {Current}/{Total} ({ElapsedMs}ms elapsed)", 
                            i + 1, contents.Count, sw.ElapsedMilliseconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("[INGEST] TIMEOUT on chunk {Index} after 15s - Bedrock call hung", i);
                    embeddings.Add(Array.Empty<float>());
                }
                catch (InvalidOperationException)
                {
                    throw; // Re-throw our fail-fast error
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[INGEST] FAILED embedding chunk {Index}: {Message}", i, ex.Message);
                    embeddings.Add(Array.Empty<float>());
                }
            }
            sw.Stop();
            _logger.LogInformation("[INGEST] All embeddings done in {ElapsedMs}ms", sw.ElapsedMilliseconds);

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
            _logger.LogInformation("[INGEST] Storing {Count} chunks in vector store...", chunks.Count);
            await _vectorStore.StoreAsync(chunks, cancellationToken);
            totalChunks += chunks.Count;
            _logger.LogInformation("[INGEST] Store complete for {FileName}", sourceUri);

            _logger.LogInformation("Ingested {ChunkCount} chunks from {FileName}", chunks.Count, file.FileName);
        }

        return new IngestResponse
        {
            ChunksIngested = totalChunks,
            SourcesProcessed = command.Files.Count
        };
    }
}
