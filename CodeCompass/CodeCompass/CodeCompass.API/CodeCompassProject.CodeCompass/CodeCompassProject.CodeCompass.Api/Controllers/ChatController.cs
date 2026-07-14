using CodeCompassProject.CodeCompass.Application.Commands;
using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CodeCompassProject.CodeCompass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ICommandHandler<SendChatMessageCommand, ChatResponse> _handler;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ICommandHandler<SendChatMessageCommand, ChatResponse> handler,
        ILogger<ChatController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Send a question to CodeCompass and receive a grounded answer with citations.
    /// </summary>
    /// <param name="request">The chat request containing the user question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grounded answer with citations from indexed documentation and code.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chat request received for session {SessionId}", request.SessionId);

        var command = new SendChatMessageCommand
        {
            Question = request.Question,
            SessionId = request.SessionId
        };

        var result = await _handler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }
}
