using CodeCompass.Core.Models;
using CodeCompass.Core.Resilience;
using CodeCompass.Search;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenSearch.Client;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 15: Vector dimension validation.
/// Vectors matching configured dimension are accepted; mismatched vectors are rejected.
///
/// **Validates: Requirements 5.6**
/// </summary>
public class VectorDimensionValidationProperty
{
    [Property(MaxTest = 100)]
    public void MatchingDimension_IsAccepted(PositiveInt dimensionSeed)
    {
        int dimension = (dimensionSeed.Get % 4096) + 1;

        var fakeClient = Substitute.For<IOpenSearchClient>();
        fakeClient.BulkAsync(Arg.Any<IBulkRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BulkResponse());

        var retryPolicy = new RetryPolicy(NullLogger.Instance);
        var store = new OpenSearchVectorStore(
            fakeClient, "test-index", batchSize: 100, embeddingDimension: dimension,
            retryPolicy, NullLogger<OpenSearchVectorStore>.Instance);

        var embedding = new float[dimension];
        Array.Fill(embedding, 0.5f);

        var document = new VectorDocument(
            Id: "test-id",
            Embedding: embedding,
            ChunkText: "test content",
            Metadata: new ChunkMetadata(
                SourceFilePath: "/test/file.cs",
                ChunkIndex: 0,
                ContentType: "code",
                Language: "csharp",
                LastModified: DateTimeOffset.UtcNow,
                SectionHeading: "TestClass"));

        Func<Task> act = () => store.UpsertAsync(new[] { document });
        act.Should().NotThrowAsync();
    }

    [Property(MaxTest = 100)]
    public void MismatchedDimension_IsRejected(PositiveInt dimensionSeed, PositiveInt vectorDimSeed)
    {
        int configuredDimension = (dimensionSeed.Get % 4096) + 1;
        int vectorDimension = (vectorDimSeed.Get % 4096) + 1;

        if (configuredDimension == vectorDimension)
            return;

        var fakeClient = Substitute.For<IOpenSearchClient>();
        var retryPolicy = new RetryPolicy(NullLogger.Instance);
        var store = new OpenSearchVectorStore(
            fakeClient, "test-index", batchSize: 100, embeddingDimension: configuredDimension,
            retryPolicy, NullLogger<OpenSearchVectorStore>.Instance);

        var embedding = new float[vectorDimension];
        Array.Fill(embedding, 0.5f);

        var document = new VectorDocument(
            Id: "test-id",
            Embedding: embedding,
            ChunkText: "test content",
            Metadata: new ChunkMetadata(
                SourceFilePath: "/test/file.cs",
                ChunkIndex: 0,
                ContentType: "code",
                Language: "csharp",
                LastModified: DateTimeOffset.UtcNow,
                SectionHeading: "TestClass"));

        Func<Task> act = () => store.UpsertAsync(new[] { document });
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*expected {configuredDimension}*got {vectorDimension}*");
    }

    [Property(MaxTest = 100)]
    public void NullEmbedding_IsRejected(PositiveInt dimensionSeed)
    {
        int dimension = (dimensionSeed.Get % 4096) + 1;

        var fakeClient = Substitute.For<IOpenSearchClient>();
        var retryPolicy = new RetryPolicy(NullLogger.Instance);
        var store = new OpenSearchVectorStore(
            fakeClient, "test-index", batchSize: 100, embeddingDimension: dimension,
            retryPolicy, NullLogger<OpenSearchVectorStore>.Instance);

        var document = new VectorDocument(
            Id: "test-id",
            Embedding: null!,
            ChunkText: "test content",
            Metadata: new ChunkMetadata(
                SourceFilePath: "/test/file.cs",
                ChunkIndex: 0,
                ContentType: "code",
                Language: "csharp",
                LastModified: DateTimeOffset.UtcNow,
                SectionHeading: "TestClass"));

        Func<Task> act = () => store.UpsertAsync(new[] { document });
        act.Should().ThrowAsync<ArgumentException>();
    }
}
