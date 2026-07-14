using CodeCompass.Core.Models;

namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class IngestKnowledgeBaseRequest
{
    public string TargetPath { get; set; } = string.Empty;
    public IndexingMode Mode { get; set; } = IndexingMode.Full;
}
