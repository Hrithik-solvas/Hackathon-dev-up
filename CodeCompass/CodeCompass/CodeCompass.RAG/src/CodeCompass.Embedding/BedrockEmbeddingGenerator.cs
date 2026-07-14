using System.Linq;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CodeCompass.Core.Configuration;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompass.Embedding;

/// <summary>
/// Generates embeddings using Amazon Bedrock (Titan Embed Text V2), with batch processing support,
/// retry logic for transient errors, and L2 normalization of output vectors.
/// </summary>
public class BedrockEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly string _modelId;
    private readonly int _batchSize;
    private readonly ILogger<BedrockEmbeddingGenerator> _logger;
    private readonly RetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance with AWS Bedrock settings and ingestion configuration.
    /// Credentials are loaded automatically from ~/.aws/credentials or environment variables.
    /// </summary>
    public BedrockEmbeddingGenerator(
        IOptions<AwsBedrockSettings> bedrockSettings,
        IOptions<IngestionSettings> ingestionSettings,
        ILogger<BedrockEmbeddingGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(bedrockSettings);
        ArgumentNullException.ThrowIfNull(ingestionSettings);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = bedrockSettings.Value;
        _modelId = settings.ModelId;
        _batchSize = ingestionSettings.Value.EmbeddingBatchSize;
        _logger = logger;
        _retryPolicy = new RetryPolicy(logger);

        _client = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(settings.Region));
    }

    /// <summary>
    /// Internal constructor for unit testing with a mock client.
    /// </summary>
    internal BedrockEmbeddingGenerator(
        AmazonBedrockRuntimeClient client,
        string modelId,
        int batchSize,
        ILogger<BedrockEmbeddingGenerator> logger,
        RetryPolicy retryPolicy)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        _batchSize = batchSize > 0 ? batchSize : 16;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        try
        {
            float[] embedding = await _retryPolicy.ExecuteAsync(
                ct => InvokeBedrockEmbeddingAsync(text, ct),
                cancellationToken).ConfigureAwait(false);

            return NormalizeVector(embedding);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Non-recoverable error generating embedding for text of length {Length}. Returning empty vector.",
                text.Length);
            return Array.Empty<float>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        _logger.LogDebug(
            "Generating embeddings for {Count} texts in batches of {BatchSize}",
            texts.Count, _batchSize);

        var allEmbeddings = new List<float[]>(texts.Count);
        var batches = CreateBatches(texts);

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Processing batch of {BatchCount} items in parallel", batch.Count);

            try
            {
                // Bedrock Titan doesn't support batch embedding in a single call,
                // so we process items in parallel within the batch for better throughput.
                var tasks = batch.Select(text =>
                    _retryPolicy.ExecuteAsync(
                        ct => InvokeBedrockEmbeddingAsync(text, ct),
                        cancellationToken)).ToArray();

                var batchResults = await Task.WhenAll(tasks).ConfigureAwait(false);

                foreach (var embedding in batchResults)
                {
                    allEmbeddings.Add(NormalizeVector(embedding));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Non-recoverable error processing batch of {BatchCount} items. Skipping batch with empty vectors.",
                    batch.Count);

                // Fill remaining items in this batch with empty vectors
                int remaining = batch.Count - (allEmbeddings.Count % _batchSize);
                for (int i = 0; i < remaining; i++)
                {
                    allEmbeddings.Add(Array.Empty<float>());
                }
            }
        }

        return allEmbeddings;
    }

    /// <summary>
    /// Calls AWS Bedrock InvokeModel with the Titan Embed Text V2 payload format.
    /// </summary>
    private async Task<float[]> InvokeBedrockEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            inputText = text,
            dimensions = 1024,
            normalize = true
        });

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload))
        };

        var response = await _client.InvokeModelAsync(request, cancellationToken).ConfigureAwait(false);

        using var responseStream = response.Body;
        var responseDoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var embeddingArray = responseDoc.RootElement.GetProperty("embedding");
        var embedding = new float[embeddingArray.GetArrayLength()];
        int index = 0;
        foreach (var element in embeddingArray.EnumerateArray())
        {
            embedding[index++] = element.GetSingle();
        }

        return embedding;
    }

    /// <summary>
    /// Splits input texts into batches of the configured batch size.
    /// </summary>
    internal IReadOnlyList<IReadOnlyList<string>> CreateBatches(IReadOnlyList<string> texts)
    {
        var batches = new List<IReadOnlyList<string>>();

        for (int i = 0; i < texts.Count; i += _batchSize)
        {
            int count = Math.Min(_batchSize, texts.Count - i);
            var batch = new List<string>(count);

            for (int j = 0; j < count; j++)
            {
                batch.Add(texts[i + j]);
            }

            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// Normalizes a vector to unit length (L2 norm = 1.0).
    /// Returns the zero vector as-is if the norm is 0.
    /// </summary>
    internal static float[] NormalizeVector(float[] vector)
    {
        double sumOfSquares = 0.0;
        for (int i = 0; i < vector.Length; i++)
        {
            sumOfSquares += (double)vector[i] * vector[i];
        }

        double norm = Math.Sqrt(sumOfSquares);

        if (norm == 0.0)
        {
            return vector;
        }

        float[] normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = (float)(vector[i] / norm);
        }

        return normalized;
    }
}
