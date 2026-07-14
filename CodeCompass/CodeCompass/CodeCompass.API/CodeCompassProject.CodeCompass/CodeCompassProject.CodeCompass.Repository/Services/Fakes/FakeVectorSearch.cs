using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;

namespace CodeCompassProject.CodeCompass.Repository.Services.Fakes;

/// <summary>
/// In-memory fake vector search for development mode.
/// Stores ingested chunks and returns them as search results based on simple text matching.
/// </summary>
public class FakeVectorSearch : IVectorSearch
{
    private static readonly List<SearchHit> _sampleHits = new()
    {
        new SearchHit(
            "A CLO (Collateralized Loan Obligation) is a type of structured credit product that pools together a portfolio of leveraged loans and issues tranches of securities with varying risk profiles. The senior tranches receive payment priority while equity tranches bear first losses.",
            0.95f,
            new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/clo_overview.md", 0, "markdown", "en", DateTimeOffset.UtcNow, "CLO Overview")),
        new SearchHit(
            "Waterfall payment structures determine how cash flows from the underlying loan portfolio are distributed across tranches. Senior tranches are paid first, followed by mezzanine and then equity. Overcollateralization tests must be passed before subordinate tranches receive payment.",
            0.91f,
            new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/waterfall.md", 1, "markdown", "en", DateTimeOffset.UtcNow, "Waterfall Mechanics")),
        new SearchHit(
            "Portfolio management in CLO involves active trading of loans within the reinvestment period, monitoring credit quality metrics, managing concentration limits, and ensuring compliance with portfolio constraints defined in the indenture.",
            0.88f,
            new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/portfolio_mgmt.md", 2, "markdown", "en", DateTimeOffset.UtcNow, "Portfolio Management")),
        new SearchHit(
            "Compliance reporting includes generating periodic trustee reports, overcollateralization ratio calculations, interest coverage tests, and regulatory submissions. Automated reporting ensures timely delivery of required disclosures.",
            0.85f,
            new ChunkMetadata("Solvas_AM_Classic_Product_Knowledge_Base/compliance.md", 3, "markdown", "en", DateTimeOffset.UtcNow, "Compliance")),
        new SearchHit(
            "React micro-frontends architecture uses Module Federation (Webpack 5) to compose independently deployable frontend applications. Each team owns a vertical slice of the UI, with shared component libraries published via a private npm registry.",
            0.93f,
            new ChunkMetadata("Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base/react_microfrontends.md", 0, "markdown", "en", DateTimeOffset.UtcNow, "React Micro-frontends")),
        new SearchHit(
            "The .NET microservices layer uses ASP.NET Core 8 minimal APIs with gRPC for inter-service communication. Each service is containerized with Docker and orchestrated via Kubernetes (AKS). Health checks and circuit breakers (Polly) ensure resilience.",
            0.90f,
            new ChunkMetadata("Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base/dotnet_microservices.md", 1, "markdown", "en", DateTimeOffset.UtcNow, ".NET Microservices")),
        new SearchHit(
            "CI/CD deployment pipelines use Azure DevOps with multi-stage YAML pipelines. Each microservice has independent build-test-deploy stages. Kubernetes deployments use Helm charts with rolling updates and automated rollback on health check failure.",
            0.87f,
            new ChunkMetadata("Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base/cicd.md", 2, "markdown", "en", DateTimeOffset.UtcNow, "CI/CD Pipelines")),
        new SearchHit(
            "Kubernetes cluster architecture uses separate node pools for system workloads and application pods. Horizontal Pod Autoscaler (HPA) manages scaling based on CPU and custom metrics. Ingress is handled via NGINX ingress controller with TLS termination.",
            0.84f,
            new ChunkMetadata("Solvas_AM_Modern_Platform_Tech_Stack_Knowledge_Base/kubernetes.md", 3, "markdown", "en", DateTimeOffset.UtcNow, "Kubernetes Architecture"))
    };

    public Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var results = _sampleHits.AsEnumerable();

        // Filter by SourcePathPrefix if provided
        if (request.Filter?.SourcePathPrefix is not null)
        {
            results = results.Where(h =>
                h.Metadata.SourceFilePath.Contains(request.Filter.SourcePathPrefix, StringComparison.OrdinalIgnoreCase));
        }

        // Simple keyword matching to make results feel relevant
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var query = request.Query.ToLowerInvariant();
            results = results
                .OrderByDescending(h => CalculateRelevance(h.ChunkText, query))
                .ThenByDescending(h => h.RelevanceScore);
        }

        var hits = results.Take(request.TopK).ToList();

        return Task.FromResult(new SearchResult(hits, hits.Count));
    }

    private static float CalculateRelevance(string text, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchCount = words.Count(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        return matchCount / (float)Math.Max(words.Length, 1);
    }
}
