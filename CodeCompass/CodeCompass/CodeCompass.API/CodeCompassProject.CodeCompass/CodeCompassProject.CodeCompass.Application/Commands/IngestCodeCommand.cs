using Microsoft.AspNetCore.Http;

namespace CodeCompassProject.CodeCompass.Application.Commands;

public class IngestCodeCommand
{
    public IFormFileCollection Files { get; set; } = null!;
    public string? RepositoryName { get; set; }
}
