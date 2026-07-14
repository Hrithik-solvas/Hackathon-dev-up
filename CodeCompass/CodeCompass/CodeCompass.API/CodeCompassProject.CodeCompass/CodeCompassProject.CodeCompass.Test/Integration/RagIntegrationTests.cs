using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Interfaces;
using CodeCompassProject.CodeCompass.Application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CodeCompassProject.CodeCompass.Test.Integration;

public class RagIntegrationTests : IClassFixture<RagIntegrationTests.MockedApiFactory>
{
    private readonly HttpClient _client;
    private readonly IVectorSearch _mockVectorSearch;
    private readonly ILLMService _mockLlmService;
    private readonly IPipelineOrchestrator _mockPipelineOrchestrator;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RagIntegrationTests(MockedApiFactory factory)
    {
        _mockVectorSearch = factory.MockVectorSearch;
        _mockLlmService = factory.MockLlmService;
        _mockPipelineOrchestrator = factory.MockPipelineOrchestrator;
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────
    // Chat endpoint tests
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task Chat_WithProductQuestion_ReturnsAnswerWithCitations()
    {
        // Arrange - "CLO" keyword triggers Product classification
        var hits = new List<SearchHit>
        {
            new("CLO stands for Collateralized Loan Obligation, a structured credit product.", 0.95f,
                new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/guide.md", 0, "markdown", "en", DateTimeOffset.UtcNow, "Overview")),
            new("Portfolio management involves tracking tranches and waterfalls.", 0.87f,
                new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/portfolio.md", 1, "markdown", "en", DateTimeOffset.UtcNow, "Portfolio"))
        };

        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResult(hits, hits.Count));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionResult
            {
                Content = "A CLO is a structured credit product used in portfolio management.",
                PromptTokens = 150,
                CompletionTokens = 30
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "What is a CLO?"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        Assert.Equal("A CLO is a structured credit product used in portfolio management.", chatResponse.Answer);
        Assert.Equal(2, chatResponse.Citations.Count);
        Assert.True(chatResponse.Citations[0].RelevanceScore >= chatResponse.Citations[1].RelevanceScore);
        Assert.Contains("Solvas_AM_Classic_Product_Knowledge_Base", chatResponse.Citations[0].SourceUri);
    }

    [Fact]
    public async Task Chat_WithTechStackQuestion_RoutesToTechStackKB()
    {
        // Arrange - "React" keyword triggers TechStack classification
        var hits = new List<SearchHit>
        {
            new("React micro-frontends use module federation for independent deployment.", 0.92f,
                new ChunkMetadata("Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base/arch.md", 0, "markdown", "en", DateTimeOffset.UtcNow, "Architecture"))
        };

        _mockVectorSearch.SearchAsync(
                Arg.Is<SearchRequest>(r => r.Filter != null && r.Filter.SourcePathPrefix!.Contains("Modern_Platform")),
                Arg.Any<CancellationToken>())
            .Returns(new SearchResult(hits, hits.Count));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionResult
            {
                Content = "The React micro-frontends use module federation.",
                PromptTokens = 100,
                CompletionTokens = 20
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "How does React micro-frontends work?"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        Assert.NotEmpty(chatResponse.Answer);
    }

    [Fact]
    public async Task Chat_WithEmptyQuestion_Returns400()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = ""
        });

        // Assert - either model validation (400) or ArgumentException mapped by middleware (400)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithWhitespaceQuestion_Returns400()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "   "
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WhenSearchReturnsNoResults_ReturnsAnswerWithEmptyCitations()
    {
        // Arrange
        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResult(new List<SearchHit>(), 0));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionResult
            {
                Content = "I don't have relevant information to answer this question.",
                PromptTokens = 50,
                CompletionTokens = 15
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "What is the meaning of life?"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        Assert.Empty(chatResponse.Citations);
        Assert.NotEmpty(chatResponse.Answer);
    }

    [Fact]
    public async Task Chat_WhenLLMReturnsEmptyContent_ReturnsFallbackAnswer()
    {
        // Arrange
        var hits = new List<SearchHit>
        {
            new("Some context chunk.", 0.8f,
                new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/doc.md", 0, "markdown", "en", DateTimeOffset.UtcNow, null))
        };

        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResult(hits, 1));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionResult { Content = "", PromptTokens = 50, CompletionTokens = 0 });

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "Tell me about compliance reporting"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        Assert.Contains("unable to generate", chatResponse.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_WhenVectorSearchThrowsHttpRequestException_Returns503()
    {
        // Arrange
        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Azure AI Search is unreachable"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "What is a CDO?"
        });

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problemDetails);
        Assert.Equal(503, problemDetails.Status);
        Assert.Contains("temporarily unavailable", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_WhenLLMTimesOut_Returns503()
    {
        // Arrange
        var hits = new List<SearchHit>
        {
            new("Some text.", 0.9f,
                new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/doc.md", 0, "markdown", "en", DateTimeOffset.UtcNow, null))
        };
        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResult(hits, 1));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("LLM request timed out"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "What is overcollateralization?"
        });

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Chat_CitationTruncatesLongChunkText()
    {
        // Arrange - chunk text > 200 chars
        var longText = new string('A', 250);
        var hits = new List<SearchHit>
        {
            new(longText, 0.88f,
                new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/doc.md", 0, "markdown", "en", DateTimeOffset.UtcNow, null))
        };

        _mockVectorSearch.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResult(hits, 1));

        _mockLlmService.GetCompletionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionResult { Content = "Answer based on context.", PromptTokens = 100, CompletionTokens = 10 });

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Question = "Tell me about trading waterfalls"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        Assert.Single(chatResponse.Citations);
        Assert.Equal(203, chatResponse.Citations[0].ChunkContent.Length); // 200 + "..."
        Assert.EndsWith("...", chatResponse.Citations[0].ChunkContent);
    }

    // ─────────────────────────────────────────────────
    // Ingestion endpoint tests
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task IngestKnowledgeBase_Success_ReturnsPipelineResult()
    {
        // Arrange
        var expectedResult = new PipelineResult(
            TotalFilesProcessed: 2,
            TotalChunksGenerated: 15,
            TotalErrors: 0,
            ElapsedMilliseconds: 1200,
            FilesNewlyIndexed: 2,
            FilesReIndexed: 0,
            FilesDeleted: 0,
            FilesSkipped: 0,
            FilesFailed: 0);

        _mockPipelineOrchestrator.RunAsync(Arg.Any<PipelineRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var response = await _client.PostAsJsonAsync("/api/ingest/knowledge-base", new IngestKnowledgeBaseRequest
        {
            TargetPath = @"C:\knowledge-bases\product",
            Mode = IndexingMode.Full
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PipelineResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalFilesProcessed);
        Assert.Equal(15, result.TotalChunksGenerated);
        Assert.Equal(0, result.TotalErrors);
    }

    [Fact]
    public async Task IngestKnowledgeBase_InvalidPath_Returns404()
    {
        // Arrange
        _mockPipelineOrchestrator.RunAsync(Arg.Any<PipelineRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DirectoryNotFoundException("Directory not found: C:\\nonexistent"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/ingest/knowledge-base", new IngestKnowledgeBaseRequest
        {
            TargetPath = @"C:\nonexistent"
        });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problemDetails);
        Assert.Equal(404, problemDetails.Status);
    }

    // ─────────────────────────────────────────────────
    // Health endpoint test
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─────────────────────────────────────────────────
    // Test Factory
    // ─────────────────────────────────────────────────

    public class MockedApiFactory : WebApplicationFactory<Program>
    {
        public IVectorSearch MockVectorSearch { get; } = Substitute.For<IVectorSearch>();
        public ILLMService MockLlmService { get; } = Substitute.For<ILLMService>();
        public IPipelineOrchestrator MockPipelineOrchestrator { get; } = Substitute.For<IPipelineOrchestrator>();
        public IEmbeddingGenerator MockEmbeddingGenerator { get; } = Substitute.For<IEmbeddingGenerator>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Provide all required config values so ConfigurationValidationHostedService doesn't throw
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureOpenAI:Endpoint"] = "https://fake-endpoint.openai.azure.com/",
                    ["AzureOpenAI:ApiKey"] = "fake-api-key",
                    ["AzureOpenAI:DeploymentName"] = "gpt-4",
                    ["AzureOpenAI:EmbeddingDeploymentName"] = "text-embedding-ada-002",
                    ["AzureSearch:Endpoint"] = "https://fake-search.search.windows.net",
                    ["AzureSearch:ApiKey"] = "fake-search-key",
                    ["AzureSearch:IndexName"] = "test-index",
                    ["KnowledgeBases:Product:SourcePathPrefix"] = "Solvas_AM_Classic_Product_Knowledge_Base",
                    ["KnowledgeBases:TechStack:SourcePathPrefix"] = "Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove real service registrations and replace with mocks
                ReplaceService<IVectorSearch>(services, MockVectorSearch);
                ReplaceService<ILLMService>(services, MockLlmService);
                ReplaceService<IPipelineOrchestrator>(services, MockPipelineOrchestrator);
                ReplaceService<IEmbeddingGenerator>(services, MockEmbeddingGenerator);

                // Mock IVectorStore (still needed by IngestDocumentsHandler, IngestCodeHandler, GetHealthHandler)
                var mockVectorStore = Substitute.For<CodeCompassProject.CodeCompass.Application.Interfaces.IVectorStore>();
                ReplaceService(services, mockVectorStore);

                // Remove services that depend on internal RAG types we can't resolve in test
                RemoveServicesByImplementationType(services, "CodeCompass.Parsing");

                // Re-register document parsers as no-op mocks
                var mockParser = Substitute.For<global::CodeCompass.Core.Interfaces.IDocumentParser>();
                services.AddSingleton(mockParser);
            });
        }

        private static void ReplaceService<T>(IServiceCollection services, T mock) where T : class
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton(mock);
        }

        private static void RemoveServicesByImplementationType(IServiceCollection services, string namespacePrefix)
        {
            var descriptors = services
                .Where(d => d.ImplementationType?.FullName?.StartsWith(namespacePrefix) == true)
                .ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
        }
    }
}
