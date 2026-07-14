namespace CodeCompassProject.CodeCompass.Application.Commands;

public class SendChatMessageCommand
{
    public string Question { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
}
