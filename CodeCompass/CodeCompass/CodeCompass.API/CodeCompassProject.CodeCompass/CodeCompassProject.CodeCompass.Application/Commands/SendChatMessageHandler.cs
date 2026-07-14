using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCompassProject.CodeCompass.Application.Commands;

public class SendChatMessageHandler : ICommandHandler<SendChatMessageCommand, ChatResponse>
{
    private readonly IVectorSearch _vectorSearch;
    private readonly IQuestionRouter _questionRouter;
    private readonly ILLMService _llmService;
    private readonly ILogger<SendChatMessageHandler> _logger;
    private readonly KnowledgeBasesSettings _knowledgeBasesSettings;

    private const string SystemPrompt = """
        You are CodeCompass, an AI Engineering Copilot. You help developers by answering questions
        about their codebase and documentation. Answer ONLY based on the provided context chunks.
        Do not use any prior knowledge. For each piece of information you reference, cite the
        originating knowledge base name (Product_Knowledge_Base or Tech_Stack_Knowledge_Base)
        as indicated in the [Source: ...] annotation of the context.
        If the context doesn't contain relevant information, say so clearly.
        """;

    public SendChatMessageHandler(
        IVectorSearch vectorSearch,
        IQuestionRouter questionRouter,
        ILLMService llmService,
        ILogger<SendChatMessageHandler> logger,
        IOptions<KnowledgeBasesSettings> knowledgeBasesSettings)
    {
        _vectorSearch = vectorSearch;
        _questionRouter = questionRouter;
        _llmService = llmService;
        _logger = logger;
        _knowledgeBasesSettings = knowledgeBasesSettings.Value;
    }

    public async Task<ChatResponse> HandleAsync(SendChatMessageCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing chat message for session {SessionId}", command.SessionId);

        // Validate input
        if (string.IsNullOrWhiteSpace(command.Question))
        {
            throw new ArgumentException("A non-empty question is required.", nameof(command.Question));
        }

        // 1. Classify the question
        var classification = _questionRouter.Classify(command.Question);
        _logger.LogInformation("Question classified as {Classification}", classification);

        // 2. Search relevant knowledge base(s) based on classification
        var searchHits = new List<SearchHit>();

        switch (classification)
        {
            case QuestionClassification.Product:
            {
                var searchRequest = new SearchRequest(
                    command.Question,
                    TopK: 5,
                    Filter: new SearchFilter(SourcePathPrefix: _knowledgeBasesSettings.Product.SourcePathPrefix));
                var searchResult = await _vectorSearch.SearchAsync(searchRequest, cancellationToken);
                searchHits.AddRange(searchResult.Hits);
                break;
            }
            case QuestionClassification.TechStack:
            {
                var searchRequest = new SearchRequest(
                    command.Question,
                    TopK: 5,
                    Filter: new SearchFilter(SourcePathPrefix: _knowledgeBasesSettings.TechStack.SourcePathPrefix));
                var searchResult = await _vectorSearch.SearchAsync(searchRequest, cancellationToken);
                searchHits.AddRange(searchResult.Hits);
                break;
            }
            case QuestionClassification.Both:
            {
                var productRequest = new SearchRequest(
                    command.Question,
                    TopK: 5,
                    Filter: new SearchFilter(SourcePathPrefix: _knowledgeBasesSettings.Product.SourcePathPrefix));
                var techStackRequest = new SearchRequest(
                    command.Question,
                    TopK: 5,
                    Filter: new SearchFilter(SourcePathPrefix: _knowledgeBasesSettings.TechStack.SourcePathPrefix));

                var productTask = _vectorSearch.SearchAsync(productRequest, cancellationToken);
                var techStackTask = _vectorSearch.SearchAsync(techStackRequest, cancellationToken);
                await Task.WhenAll(productTask, techStackTask);

                var productResult = await productTask;
                var techStackResult = await techStackTask;

                searchHits = productResult.Hits
                    .Concat(techStackResult.Hits)
                    .OrderByDescending(h => h.RelevanceScore)
                    .Take(5)
                    .ToList();
                break;
            }
        }

        _logger.LogInformation("Retrieved {ChunkCount} relevant chunks", searchHits.Count);

        // 3. Build annotated context from chunks with knowledge base origin
        var contextChunks = searchHits.Select(hit =>
        {
            var kbName = DetermineKnowledgeBaseOrigin(hit.Metadata.SourceFilePath);
            return $"[Source: {kbName}] {hit.ChunkText}";
        }).ToList();

        // 4. Call LLM with annotated context (even if empty — handles zero search results)
        var completionResult = await _llmService.GetCompletionAsync(
            SystemPrompt,
            command.Question,
            contextChunks,
            cancellationToken);

        // 5. Handle empty LLM content with fallback message
        var answer = string.IsNullOrEmpty(completionResult.Content)
            ? "I was unable to generate a response from the provided context."
            : completionResult.Content;

        // 6. Build citations from search hits
        var citations = searchHits.Select(hit => new CitationDto
        {
            SourceUri = hit.Metadata.SourceFilePath,
            ChunkContent = string.IsNullOrEmpty(hit.ChunkText)
                ? string.Empty
                : hit.ChunkText.Length > 200
                    ? hit.ChunkText[..200] + "..."
                    : hit.ChunkText,
            RelevanceScore = hit.RelevanceScore
        }).OrderByDescending(c => c.RelevanceScore).ToList();

        var sessionId = command.SessionId ?? Guid.NewGuid();

        _logger.LogInformation("Chat response generated. Tokens used: {PromptTokens}/{CompletionTokens}",
            completionResult.PromptTokens, completionResult.CompletionTokens);

        return new ChatResponse
        {
            Answer = answer,
            Citations = citations,
            SessionId = sessionId
        };
    }

    private string DetermineKnowledgeBaseOrigin(string sourceFilePath)
    {
        if (!string.IsNullOrEmpty(_knowledgeBasesSettings.Product.SourcePathPrefix) &&
            sourceFilePath.Contains(_knowledgeBasesSettings.Product.SourcePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Product_Knowledge_Base";
        }

        if (!string.IsNullOrEmpty(_knowledgeBasesSettings.TechStack.SourcePathPrefix) &&
            sourceFilePath.Contains(_knowledgeBasesSettings.TechStack.SourcePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Tech_Stack_Knowledge_Base";
        }

        // Default to Product_Knowledge_Base if no prefix match (shouldn't happen with proper configuration)
        return "Product_Knowledge_Base";
    }
}
