using CodeCompass.Core.Models;

namespace CodeCompass.Core.Interfaces;

/// <summary>
/// Extracts and enriches metadata from parsed content.
/// </summary>
public interface IMetadataExtractor
{
    ChunkMetadata ExtractDocumentMetadata(ParsedDocument document, int chunkIndex, string? nearestHeading);
    ChunkMetadata ExtractCodeMetadata(ParsedCode code, int chunkIndex, string? containingSymbol);
}
