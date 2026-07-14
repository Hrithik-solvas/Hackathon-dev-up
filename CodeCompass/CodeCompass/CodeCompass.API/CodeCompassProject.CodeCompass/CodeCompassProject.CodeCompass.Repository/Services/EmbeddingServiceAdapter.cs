using CodeCompass.Core.Interfaces;
using CodeCompassProject.CodeCompass.Application.Interfaces;

namespace CodeCompassProject.CodeCompass.Repository.Services;

public class EmbeddingServiceAdapter : IEmbeddingService
{
    private readonly IEmbeddingGenerator _embeddingGenerator;

    public EmbeddingServiceAdapter(IEmbeddingGenerator embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        => _embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken);

    public async Task<IEnumerable<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        var results = await _embeddingGenerator.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        return results;
    }
}
