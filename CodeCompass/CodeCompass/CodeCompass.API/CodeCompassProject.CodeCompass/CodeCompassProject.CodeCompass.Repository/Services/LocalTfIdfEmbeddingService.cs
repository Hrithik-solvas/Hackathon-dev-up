using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Local embedding service using a deterministic hash-based approach that produces
/// consistent vectors for similar text. No external API calls needed.
/// Uses character n-gram hashing to create sparse-like vectors that capture text similarity.
/// </summary>
public class LocalTfIdfEmbeddingService : IEmbeddingService
{
    private const int EmbeddingDimension = 1024;
    private readonly ILogger<LocalTfIdfEmbeddingService> _logger;

    public LocalTfIdfEmbeddingService(ILogger<LocalTfIdfEmbeddingService> logger)
    {
        _logger = logger;
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = GenerateEmbedding(text);
        return Task.FromResult(embedding);
    }

    public Task<IEnumerable<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var results = texts.Select(GenerateEmbedding);
        return Task.FromResult(results);
    }

    private float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[EmbeddingDimension];

        var vector = new float[EmbeddingDimension];

        // Normalize text
        var normalized = NormalizeText(text);

        // Extract tokens (words)
        var words = Regex.Split(normalized, @"\W+")
            .Where(w => w.Length > 1)
            .ToList();

        // Generate word-level features
        foreach (var word in words)
        {
            // Hash each word to a position in the vector
            var hash = GetStableHash(word);
            var position = (int)(((uint)hash) % EmbeddingDimension);
            vector[position] += 1.0f;

            // Also hash bigrams (pairs of characters) for sub-word matching
            for (int i = 0; i < word.Length - 1; i++)
            {
                var bigram = word.Substring(i, 2);
                var bigramHash = GetStableHash(bigram);
                var bigramPos = (int)(((uint)bigramHash) % EmbeddingDimension);
                vector[bigramPos] += 0.5f;
            }
        }

        // Generate word bigrams (adjacent word pairs) for phrase matching
        for (int i = 0; i < words.Count - 1; i++)
        {
            var pair = words[i] + "_" + words[i + 1];
            var pairHash = GetStableHash(pair);
            var pairPos = (int)(((uint)pairHash) % EmbeddingDimension);
            vector[pairPos] += 0.75f;
        }

        // L2 normalize
        var norm = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= norm;
        }

        return vector;
    }

    private static string NormalizeText(string text)
    {
        return text.ToLowerInvariant()
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
    }

    /// <summary>
    /// Produces a stable hash that is consistent across runs (unlike GetHashCode).
    /// </summary>
    private static int GetStableHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return BitConverter.ToInt32(hashBytes, 0);
    }
}
