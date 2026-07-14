namespace CodeCompassProject.CodeCompass.Repository.Configuration;

public class VectorStoreSettings
{
    public const string SectionName = "VectorStore";

    public string Type { get; set; } = "InMemory";
    public string ConnectionString { get; set; } = string.Empty;
}
