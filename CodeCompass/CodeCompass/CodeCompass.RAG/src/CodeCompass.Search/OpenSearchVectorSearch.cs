using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using SearchRequest = CodeCompass.Core.Models.SearchRequest;

namespace CodeCompass.Search;

/// <summary>
/// Amazon OpenSearch implementation of IVectorSearch.
/// Generates a query embedding and executes k-NN vector similarity search with optional metadata filtering.
/// </summary>
public class OpenSearchVectorSearch : IVectorSearch
{
    private const int MinQueryLength = 1;
    private const int MaxQueryLength = 2000;
    private const int MinTopK = 1;
    private const int MaxTopK = 50;

    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IOpenSearchClient _client;
    private readonly string _indexName;
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<OpenSearchVectorSearch> _logger;

    /// <summary>
    /// Initializes a new instance using configuration settings.
    /// </summary>
    public OpenSearchVectorSearch(
        IEmbeddingGenerator embeddingGenerator,
        IOptions<OpenSearchSettings> searchSettings,
        RetryPolicy retryPolicy,
        ILogger<OpenSearchVectorSearch> logger)
    {
        ArgumentNullException.ThrowIfNull(embeddingGenerator);
        ArgumentNullException.ThrowIfNull(searchSettings);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = searchSettings.Value;
        _embeddingGenerator = embeddingGenerator;
        _indexName = settings.IndexName;
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
    internal OpenSearchVectorSearch(
        IEmbeddingGenerator embeddingGenerator,
        IOpenSearchClient client,
        string indexName,
        RetryPolicy retryPolicy,
        ILogger<OpenSearchVectorSearch> logger)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrEmpty(request.Query) || request.Query.Length < MinQueryLength)
        {
            throw new ArgumentException(
                $"Query must be between {MinQueryLength} and {MaxQueryLength} characters. Actual: {request.Query?.Length ?? 0}",
                nameof(request));
        }

        if (request.Query.Length > MaxQueryLength)
        {
            throw new ArgumentException(
                $"Query must be between {MinQueryLength} and {MaxQueryLength} characters. Actual: {request.Query.Length}",
                nameof(request));
        }

        if (request.TopK < MinTopK || request.TopK > MaxTopK)
        {
            throw new ArgumentException(
                $"TopK must be between {MinTopK} and {MaxTopK}. Actual: {request.TopK}",
                nameof(request));
        }

        _logger.LogInformation(
            "Executing vector search: query length={QueryLength}, topK={TopK}, hasFilter={HasFilter}",
            request.Query.Length, request.TopK, request.Filter != null);

        // Generate query embedding
        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _retryPolicy.ExecuteAsync(
                ct => _embeddingGenerator.GenerateEmbeddingAsync(request.Query, ct),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate query embedding for query of length {QueryLength}.", request.Query.Length);
            return new SearchResult(Hits: Array.Empty<SearchHit>(), TotalCount: 0);
        }

        // Execute k-NN search
        try
        {
            var searchHits = await _retryPolicy.ExecuteAsync(
                async ct =>
                {
                    var searchDescriptor = new SearchDescriptor<OpenSearchDocument>()
                        .Index(_indexName)
                        .Size(request.TopK)
                        .Query(q => BuildKnnQuery(q, queryEmbedding, request.TopK, request.Filter));

                    var response = await _client.SearchAsync<OpenSearchDocument>(searchDescriptor, ct).ConfigureAwait(false);

                    if (!response.IsValid)
                    {
                        _logger.LogWarning("OpenSearch query failed: {Error}", response.ServerError?.Error?.Reason ?? "Unknown");
                        return Array.Empty<SearchHit>();
                    }

                    return response.Hits
                        .Select(h => new SearchHit(
                            ChunkText: h.Source.ChunkText,
                            RelevanceScore: (float)(h.Score ?? 0.0),
                            Metadata: new ChunkMetadata(
                                SourceFilePath: h.Source.SourceFilePath,
                                ChunkIndex: h.Source.ChunkIndex,
                                ContentType: h.Source.ContentType,
                                Language: string.IsNullOrEmpty(h.Source.Language) ? null : h.Source.Language,
                                LastModified: h.Source.LastModified,
                                SectionHeading: string.IsNullOrEmpty(h.Source.SectionHeading) ? null : h.Source.SectionHeading)))
                        .OrderByDescending(h => h.RelevanceScore)
                        .ToArray();
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Vector search completed: {HitCount} results returned.", searchHits.Length);
            return new SearchResult(Hits: searchHits, TotalCount: searchHits.Length);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed for query of length {QueryLength}.", request.Query.Length);
            return new SearchResult(Hits: Array.Empty<SearchHit>(), TotalCount: 0);
        }
    }

    /// <summary>
    /// Builds an OpenSearch k-NN query with optional metadata filters.
    /// </summary>
    private QueryContainer BuildKnnQuery(
        QueryContainerDescriptor<OpenSearchDocument> q,
        float[] queryVector,
        int k,
        SearchFilter? filter)
    {
        var knnQuery = q.Knn(knn => knn
            .Field(f => f.Embedding)
            .Vector(queryVector)
            .K(k));

        if (filter == null)
        {
            return knnQuery;
        }

        var filterQueries = new List<QueryContainer>();

        if (!string.IsNullOrEmpty(filter.ContentType))
        {
            filterQueries.Add(q.Term(t => t.Field(f => f.ContentType).Value(filter.ContentType)));
        }

        if (!string.IsNullOrEmpty(filter.Language))
        {
            filterQueries.Add(q.Term(t => t.Field(f => f.Language).Value(filter.Language)));
        }

        if (!string.IsNullOrEmpty(filter.SourcePathPrefix))
        {
            filterQueries.Add(q.Prefix(p => p.Field(f => f.SourceFilePath).Value(filter.SourcePathPrefix)));
        }

        if (filterQueries.Count == 0)
        {
            return knnQuery;
        }

        return q.Bool(b => b
            .Must(knnQuery)
            .Filter(filterQueries.ToArray()));
    }
}
