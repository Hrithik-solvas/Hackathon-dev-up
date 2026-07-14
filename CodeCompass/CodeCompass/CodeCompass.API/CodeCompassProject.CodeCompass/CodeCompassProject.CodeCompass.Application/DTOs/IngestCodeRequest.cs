using Microsoft.AspNetCore.Http;

namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class IngestCodeRequest
{
    public IFormFileCollection Files { get; set; } = null!;
    public string? RepositoryName { get; set; }
}
