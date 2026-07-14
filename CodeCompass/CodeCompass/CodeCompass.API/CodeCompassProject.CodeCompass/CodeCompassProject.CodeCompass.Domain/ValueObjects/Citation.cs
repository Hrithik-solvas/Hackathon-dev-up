namespace CodeCompassProject.CodeCompass.Domain.Entities;

public class Citation
{
    public string SourceUri { get; set; } = string.Empty;
    public string ChunkContent { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
