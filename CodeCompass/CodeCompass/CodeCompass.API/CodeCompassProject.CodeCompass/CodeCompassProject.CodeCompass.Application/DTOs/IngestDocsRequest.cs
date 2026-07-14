using Microsoft.AspNetCore.Http;

namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class IngestDocsRequest
{
    public IFormFileCollection Files { get; set; } = null!;
    public Dictionary<string, string>? Metadata { get; set; }
}
