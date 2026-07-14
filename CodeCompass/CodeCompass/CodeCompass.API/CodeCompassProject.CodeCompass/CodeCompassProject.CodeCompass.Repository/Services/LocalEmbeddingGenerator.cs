using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CodeCompass.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Local IEmbeddingGenerator implementation using TF-IDF hash-based approach.
/// Compatible with the RAG pipeline's IEmbeddingGenerator interface.
/// No external API calls needed.
/// </summary>
public class LocalEmbeddingGenerator : IEmbeddingGenerator
{
    private const int EmbeddingDimension = 1024;
    private readonly ILogger<LocalEmbeddingGenerator> _logger;

    public LocalEmbeddingGenerator(ILogger<LocalEmbeddingGenerator> logger)
    {
        _logger = logger;
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = GenerateEmbedding(text);
        return Task.FromResult(embedding);
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = texts.Select(GenerateEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(results);
    }

    private float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[EmbeddingDimension];

        var vector = new float[EmbeddingDimension];

        // Normalize text
        var normalized = text.ToLowerInvariant()
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        // Extract tokens (words)
        var words = Regex.Split(normalized, @"\W+")
            .Where(w => w.Length > 1)
            .ToList();

        // Generate word-level features
        foreach (var word in words)
        {
            var hash = GetStableHash(word);
            var position = (int)(((uint)hash) % EmbeddingDimension);
            vector[position] += 1.0f;

            // Character bigrams for sub-word matching
            for (int i = 0; i < word.Length - 1; i++)
            {
                var bigram = word.Substring(i, 2);
                var bigramHash = GetStableHash(bigram);
                var bigramPos = (int)(((uint)bigramHash) % EmbeddingDimension);
                vector[bigramPos] += 0.5f;
            }
        }

        // Word bigrams for phrase matching
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

    private static int GetStableHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return BitConverter.ToInt32(hashBytes, 0);
    }
}
