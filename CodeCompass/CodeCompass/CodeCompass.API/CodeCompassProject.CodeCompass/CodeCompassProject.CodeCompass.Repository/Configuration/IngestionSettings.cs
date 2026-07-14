namespace CodeCompassProject.CodeCompass.Repository.Configuration;

public class IngestionSettings
{
    public const string SectionName = "Ingestion";

    public int MaxChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
}
