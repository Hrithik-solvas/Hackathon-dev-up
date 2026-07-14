namespace CodeCompassProject.CodeCompass.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public float[] EmbeddingVector { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
