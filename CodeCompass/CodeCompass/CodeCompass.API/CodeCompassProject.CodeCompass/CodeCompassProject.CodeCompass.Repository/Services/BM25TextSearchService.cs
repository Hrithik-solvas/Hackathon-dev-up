using System.Net.Http.Json;
using System.Text.Json;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeCompass.Core.Configuration;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// BM25-based text search that queries OpenSearch using full-text search instead of vector similarity.
/// Provides much better results than hash-based embeddings for keyword matching.
/// </summary>
public class BM25TextSearchService : IVectorSearch
{
    private readonly HttpClient _httpClient;
    private readonly string _indexName;
    private readonly ILogger<BM25TextSearchService> _logger;

    public BM25TextSearchService(
        IOptions<OpenSearchSettings> searchSettings,
        ILogger<BM25TextSearchService> logger)
    {
        _logger = logger;
        var settings = searchSettings.Value;
        _indexName = settings.IndexName;
        _httpClient = new HttpClient { BaseAddress = new Uri(settings.Endpoint) };
    }

    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchResult(new List<SearchHit>(), 0);
        }

        _logger.LogInformation("[BM25] Searching for: \"{Query}\" (topK={TopK})", request.Query, request.TopK);

        try
        {
            // Use OpenSearch BM25 full-text search on the chunkText field
            var searchBody = new
            {
                size = request.TopK,
                query = new
                {
                    @bool = new
                    {
                        should = new object[]
                        {
                            // Main text match with boosting
                            new
                            {
                                match = new Dictionary<string, object>
                                {
                                    ["chunkText"] = new { query = request.Query, boost = 2.0 }
                                }
                            },
                            // Phrase match for better relevance when words appear together
                            new
                            {
                                match_phrase = new Dictionary<string, object>
                                {
                                    ["chunkText"] = new { query = request.Query, boost = 3.0 }
                                }
                            }
                        },
                        minimum_should_match = 1
                    }
                },
                _source = new[] { "chunkText", "sourceFilePath", "chunkIndex", "contentType", "language", "sectionHeading" }
            };

            var jsonContent = JsonSerializer.Serialize(searchBody);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_indexName}/_search", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[BM25] OpenSearch returned {StatusCode}: {Error}", response.StatusCode, error);
                return new SearchResult(new List<SearchHit>(), 0);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var hits = new List<SearchHit>();

            if (responseJson?.RootElement.TryGetProperty("hits", out var hitsObj) == true
                && hitsObj.TryGetProperty("hits", out var hitsArray))
            {
                foreach (var hit in hitsArray.EnumerateArray())
                {
                    var score = hit.TryGetProperty("_score", out var scoreProp) ? (float)scoreProp.GetDouble() : 0.0f;
                    var source = hit.GetProperty("_source");

                    var chunkText = source.TryGetProperty("chunkText", out var ct) ? ct.GetString() ?? "" : "";
                    var sourceFilePath = source.TryGetProperty("sourceFilePath", out var sfp) ? sfp.GetString() ?? "" : "";
                    var chunkIndex = source.TryGetProperty("chunkIndex", out var ci) && ci.ValueKind == JsonValueKind.Number ? ci.GetInt32() : 0;
                    var contentType = source.TryGetProperty("contentType", out var ctp) ? ctp.GetString() ?? "" : "";
                    var language = source.TryGetProperty("language", out var lang) ? lang.GetString() : null;
                    var sectionHeading = source.TryGetProperty("sectionHeading", out var sh) ? sh.GetString() : null;

                    hits.Add(new SearchHit(
                        ChunkText: chunkText,
                        RelevanceScore: score,
                        Metadata: new ChunkMetadata(
                            SourceFilePath: sourceFilePath,
                            ChunkIndex: chunkIndex,
                            ContentType: contentType,
                            Language: language,
                            LastModified: DateTimeOffset.UtcNow,
                            SectionHeading: sectionHeading)));
                }
            }

            _logger.LogInformation("[BM25] Found {Count} results", hits.Count);
            return new SearchResult(hits, hits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BM25] Search failed: {Message}", ex.Message);
            return new SearchResult(new List<SearchHit>(), 0);
        }
    }
}
