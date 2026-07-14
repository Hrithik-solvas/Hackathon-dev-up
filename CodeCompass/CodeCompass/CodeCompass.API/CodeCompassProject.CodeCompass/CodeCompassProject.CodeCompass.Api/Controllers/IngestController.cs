using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompassProject.CodeCompass.Application.Commands;
using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CodeCompassProject.CodeCompass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly ICommandHandler<IngestDocumentsCommand, IngestResponse> _ingestDocsHandler;
    private readonly ICommandHandler<IngestCodeCommand, IngestResponse> _ingestCodeHandler;
    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private readonly ILogger<IngestController> _logger;

    public IngestController(
        ICommandHandler<IngestDocumentsCommand, IngestResponse> ingestDocsHandler,
        ICommandHandler<IngestCodeCommand, IngestResponse> ingestCodeHandler,
        IPipelineOrchestrator pipelineOrchestrator,
        ILogger<IngestController> logger)
    {
        _ingestDocsHandler = ingestDocsHandler;
        _ingestCodeHandler = ingestCodeHandler;
        _pipelineOrchestrator = pipelineOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Ingest documentation files into the vector store.
    /// </summary>
    /// <param name="files">The documentation files to ingest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of ingested chunks.</returns>
    [HttpPost("docs")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestResponse>> IngestDocs(
        [FromForm] IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No files provided",
                Detail = "At least one file must be uploaded for ingestion."
            });
        }

        _logger.LogInformation("Ingesting {FileCount} documentation file(s)", files.Count);

        var command = new IngestDocumentsCommand
        {
            Files = files
        };

        var result = await _ingestDocsHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ingest source code files into the vector store.
    /// </summary>
    /// <param name="files">The code files to ingest.</param>
    /// <param name="repositoryName">Optional repository name for source tracking.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of ingested chunks.</returns>
    [HttpPost("code")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestResponse>> IngestCode(
        [FromForm] IFormFileCollection files,
        [FromForm] string? repositoryName,
        CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No files provided",
                Detail = "At least one code file must be uploaded for ingestion."
            });
        }

        _logger.LogInformation("Ingesting {FileCount} code file(s) from {Repo}", files.Count, repositoryName ?? "unknown");

        var command = new IngestCodeCommand
        {
            Files = files,
            RepositoryName = repositoryName
        };

        var result = await _ingestCodeHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ingest a knowledge base directory into the Azure AI Search index via the RAG pipeline.
    /// </summary>
    /// <param name="request">The ingestion request containing target path and indexing mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pipeline result with processing summary.</returns>
    [HttpPost("knowledge-base")]
    [ProducesResponseType(typeof(PipelineResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PipelineResult>> IngestKnowledgeBase(
        [FromBody] IngestKnowledgeBaseRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ingesting knowledge base from {TargetPath} with mode {Mode}", request.TargetPath, request.Mode);

        var pipelineRequest = new PipelineRequest(request.TargetPath, request.Mode);
        var result = await _pipelineOrchestrator.RunAsync(pipelineRequest, cancellationToken);
        return Ok(result);
    }
}
