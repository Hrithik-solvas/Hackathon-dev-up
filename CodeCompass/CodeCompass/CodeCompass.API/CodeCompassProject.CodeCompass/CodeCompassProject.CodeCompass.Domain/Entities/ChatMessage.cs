namespace CodeCompassProject.CodeCompass.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
