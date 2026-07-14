using Microsoft.AspNetCore.Http;

namespace CodeCompassProject.CodeCompass.Application.Commands;

public class IngestDocumentsCommand
{
    public IFormFileCollection Files { get; set; } = null!;
    public Dictionary<string, string>? Metadata { get; set; }
}
