using CodeCompassProject.CodeCompass.Domain.Entities;

namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = new();
    public Guid SessionId { get; set; }
}

public class CitationDto
{
    public string SourceUri { get; set; } = string.Empty;
    public string ChunkContent { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
