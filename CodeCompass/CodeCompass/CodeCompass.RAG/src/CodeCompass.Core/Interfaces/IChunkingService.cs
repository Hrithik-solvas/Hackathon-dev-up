using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Splits parsed content into semantically coherent chunks respecting logical boundaries.
/// </summary>
public interface IChunkingService
{
    IReadOnlyList<Chunk> ChunkDocument(ParsedDocument document, ChunkingOptions? options = null);
    IReadOnlyList<Chunk> ChunkCode(ParsedCode code, ChunkingOptions? options = null);
}
