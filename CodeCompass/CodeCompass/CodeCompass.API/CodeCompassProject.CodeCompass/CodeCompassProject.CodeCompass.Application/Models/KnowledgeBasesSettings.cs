namespace CodeCompassProject.CodeCompass.Application.Models;

public class KnowledgeBasesSettings
{
    public const string SectionName = "KnowledgeBases";
    public KnowledgeBaseEntry Product { get; set; } = new();
    public KnowledgeBaseEntry TechStack { get; set; } = new();
}

public class KnowledgeBaseEntry
{
    public string SourcePathPrefix { get; set; } = string.Empty;
}
