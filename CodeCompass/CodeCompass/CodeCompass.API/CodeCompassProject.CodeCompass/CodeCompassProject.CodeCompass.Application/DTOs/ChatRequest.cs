using System.ComponentModel.DataAnnotations;

namespace CodeCompassProject.CodeCompass.Application.DTOs;

public class ChatRequest
{
    [Required]
    [MinLength(1)]
    public string Question { get; set; } = string.Empty;

    public Guid? SessionId { get; set; }
}
