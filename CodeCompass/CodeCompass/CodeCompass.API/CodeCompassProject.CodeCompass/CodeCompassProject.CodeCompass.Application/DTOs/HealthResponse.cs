namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public List<ServiceHealth> Services { get; set; } = new();
}

public class ServiceHealth
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
}
