using System.Security.Cryptography;
using System.Text;
using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace CodeCompass.Search;

/// <summary>
/// Amazon OpenSearch implementation of IVectorStore.
/// Handles batched upserts with deterministic document ID generation.
/// </summary>
public class OpenSearchVectorStore : IVectorStore
{
    private readonly IOpenSearchClient _client;
    private readonly string _indexName;
    private readonly int _batchSize;
    private readonly int _embeddingDimension;
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<OpenSearchVectorStore> _logger;

    /// <summary>
    /// Initializes a new instance using configuration settings.
    /// Credentials are loaded automatically from ~/.aws/credentials or environment variables.
    /// </summary>
    public OpenSearchVectorStore(
        IOptions<OpenSearchSettings> searchSettings,
        IOptions<AwsBedrockSettings> bedrockSettings,
        RetryPolicy retryPolicy,
        ILogger<OpenSearchVectorStore> logger)
    {
        ArgumentNullException.ThrowIfNull(searchSettings);
        ArgumentNullException.ThrowIfNull(bedrockSettings);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = searchSettings.Value;
        _indexName = settings.IndexName;
        _batchSize = settings.BatchSize;
        _embeddingDimension = bedrockSettings.Value.EmbeddingDimension;
        _retryPolicy = retryPolicy;
        _logger = logger;

        var connectionSettings = new ConnectionSettings(new Uri(settings.Endpoint))
            .DefaultIndex(_indexName)
            .EnableHttpCompression();

        _client = new OpenSearchClient(connectionSettings);
    }

    /// <summary>
    /// Internal constructor for unit testing with a mock client.
    /// </summary>
    internal OpenSearchVectorStore(
        IOpenSearchClient client,
        string indexName,
        int batchSize,
        int embeddingDimension,
        RetryPolicy retryPolicy,
        ILogger<OpenSearchVectorStore> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _batchSize = batchSize > 0 ? batchSize : 100;
        _embeddingDimension = embeddingDimension > 0 ? embeddingDimension : 1024;
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
    {
        if (documents == null || documents.Count == 0)
        {
            _logger.LogDebug("No documents to upsert.");
            return;
        }

        ValidateVectorDimensions(documents);

        var batches = CreateBatches(documents);
        _logger.LogInformation(
            "Upserting {DocumentCount} documents in {BatchCount} batch(es) of up to {BatchSize}.",
            documents.Count, batches.Count, _batchSize);

        for (int i = 0; i < batches.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = batches[i];
            _logger.LogDebug(
                "Submitting batch {BatchIndex}/{BatchCount} with {DocumentCount} documents.",
                i + 1, batches.Count, batch.Count);

            try
            {
                await _retryPolicy.ExecuteAsync(
                    async ct =>
                    {
                        var bulkDescriptor = new BulkDescriptor(_indexName);

                        foreach (var doc in batch)
                        {
                            var id = GenerateDocumentId(doc.Metadata.SourceFilePath, doc.Metadata.ChunkIndex);
                            var indexDoc = new OpenSearchDocument
                            {
                                Id = id,
                                ChunkText = doc.ChunkText,
                                Embedding = doc.Embedding,
                                SourceFilePath = doc.Metadata.SourceFilePath,
                                ChunkIndex = doc.Metadata.ChunkIndex,
                                ContentType = doc.Metadata.ContentType,
                                Language = doc.Metadata.Language ?? string.Empty,
                                LastModified = doc.Metadata.LastModified,
                                SectionHeading = doc.Metadata.SectionHeading ?? string.Empty
                            };

                            bulkDescriptor.Index<OpenSearchDocument>(op => op
                                .Document(indexDoc)
                                .Id(id));
                        }

                        var response = await _client.BulkAsync(bulkDescriptor, ct).ConfigureAwait(false);

                        if (response.Errors)
                        {
                            var errorItems = response.ItemsWithErrors.ToList();
                            _logger.LogWarning(
                                "Bulk upsert had {ErrorCount} errors in batch {BatchIndex}.",
                                errorItems.Count, i + 1);
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                foreach (var doc in batch)
                {
                    _logger.LogError(
                        ex,
                        "Final failure upserting chunk: source file '{SourceFilePath}', chunk index {ChunkIndex}.",
                        doc.Metadata.SourceFilePath,
                        doc.Metadata.ChunkIndex);
                }
            }
        }

        _logger.LogInformation("Completed upsert operation for {DocumentCount} documents.", documents.Count);
    }

    /// <inheritdoc />
    public async Task DeleteBySourceFileAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            _logger.LogWarning("DeleteBySourceFileAsync called with null or empty source file path.");
            return;
        }

        _logger.LogInformation("Deleting all chunks for source file: {SourceFilePath}", sourceFilePath);

        try
        {
            await _retryPolicy.ExecuteAsync(
                async ct =>
                {
                    var response = await _client.DeleteByQueryAsync<OpenSearchDocument>(d => d
                        .Index(_indexName)
                        .Query(q => q
                            .Term(t => t
                                .Field(f => f.SourceFilePath)
                                .Value(sourceFilePath))),
                        ct).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Deleted {DeletedCount} document(s) for source file: {SourceFilePath}",
                        response.Deleted, sourceFilePath);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Final failure deleting documents for source file '{SourceFilePath}'.",
                sourceFilePath);
        }
    }

    /// <summary>
    /// Generates a deterministic document ID from a source file path and chunk index.
    /// </summary>
    public static string GenerateDocumentId(string sourceFilePath, int chunkIndex)
    {
        string input = $"{sourceFilePath}|{chunkIndex}";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private void ValidateVectorDimensions(IReadOnlyList<VectorDocument> documents)
    {
        foreach (var doc in documents)
        {
            if (doc.Embedding == null || doc.Embedding.Length != _embeddingDimension)
            {
                int actualDimension = doc.Embedding?.Length ?? 0;
                throw new ArgumentException(
                    $"Vector dimension mismatch for source file '{doc.Metadata.SourceFilePath}', chunk index {doc.Metadata.ChunkIndex}: " +
                    $"expected {_embeddingDimension}, got {actualDimension}.");
            }
        }
    }

    internal List<List<VectorDocument>> CreateBatches(IReadOnlyList<VectorDocument> documents)
    {
        var batches = new List<List<VectorDocument>>();
        for (int i = 0; i < documents.Count; i += _batchSize)
        {
            int count = Math.Min(_batchSize, documents.Count - i);
            var batch = new List<VectorDocument>(count);
            for (int j = 0; j < count; j++)
            {
                batch.Add(documents[i + j]);
            }
            batches.Add(batch);
        }
        return batches;
    }
}

/// <summary>
/// Internal document model for OpenSearch indexing.
/// </summary>
internal class OpenSearchDocument
{
    public string Id { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string SourceFilePath { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    public string SectionHeading { get; set; } = string.Empty;
}
