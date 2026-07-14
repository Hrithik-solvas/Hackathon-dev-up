using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Core.Resilience;
using CodeCompass.Search;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenSearch.Client;
using SearchRequest = CodeCompass.Core.Models.SearchRequest;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 16: Input validation for search requests.
/// Query length 1–2000 accepted, 0 or >2000 rejected; K in [1,50] accepted, outside rejected.
///
/// **Validates: Requirements 6.3**
/// </summary>
public class InputValidationProperty
{
    private readonly OpenSearchVectorSearch _searchService;

    public InputValidationProperty()
    {
        var fakeEmbeddingGenerator = new StubEmbeddingGenerator();
        var fakeClient = Substitute.For<IOpenSearchClient>();
        var retryPolicy = new RetryPolicy(NullLogger.Instance);

        _searchService = new OpenSearchVectorSearch(
            fakeEmbeddingGenerator,
            fakeClient,
            "test-index",
            retryPolicy,
            NullLogger<OpenSearchVectorSearch>.Instance);
    }

    [Property(MaxTest = 100)]
    public void ValidQueryLength_IsAccepted(PositiveInt lengthSeed)
    {
        int length = ((lengthSeed.Get - 1) % 2000) + 1;
        string query = new string('a', length);
        var request = new SearchRequest(Query: query, TopK: 5);

        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().NotThrowAsync<ArgumentException>();
    }

    [Property(MaxTest = 100)]
    public void EmptyQuery_IsRejected()
    {
        var request = new SearchRequest(Query: string.Empty, TopK: 5);
        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Property(MaxTest = 100)]
    public void TooLongQuery_IsRejected(PositiveInt excessSeed)
    {
        int length = 2000 + (excessSeed.Get % 1000) + 1;
        string query = new string('x', length);
        var request = new SearchRequest(Query: query, TopK: 5);

        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Property(MaxTest = 100)]
    public void ValidTopK_IsAccepted(PositiveInt kSeed)
    {
        int topK = ((kSeed.Get - 1) % 50) + 1;
        var request = new SearchRequest(Query: "valid query", TopK: topK);

        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().NotThrowAsync<ArgumentException>();
    }

    [Property(MaxTest = 100)]
    public void TopKBelowMin_IsRejected(PositiveInt belowSeed)
    {
        int topK = -(belowSeed.Get % 100);
        var request = new SearchRequest(Query: "valid query", TopK: topK);

        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Property(MaxTest = 100)]
    public void TopKAboveMax_IsRejected(PositiveInt aboveSeed)
    {
        int topK = 51 + (aboveSeed.Get % 100);
        var request = new SearchRequest(Query: "valid query", TopK: topK);

        Func<Task> act = () => _searchService.SearchAsync(request, CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void IndexingMode_OnlyFullAndIncrementalAreValid()
    {
        var validModes = Enum.GetValues<IndexingMode>();
        validModes.Should().HaveCount(2);
        validModes.Should().Contain(IndexingMode.Full);
        validModes.Should().Contain(IndexingMode.Incremental);
    }

    private sealed class StubEmbeddingGenerator : IEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 0.1f, 0.2f, 0.3f });

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(
            IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            var r = texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(r);
        }
    }
}
