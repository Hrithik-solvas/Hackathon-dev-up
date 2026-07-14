using CodeCompassProject.CodeCompass.Application.Interfaces;

namespace CodeCompassProject.CodeCompass.Repository.Services;

/// <summary>
/// Keyword-based question router that classifies questions by matching against
/// domain-specific keyword sets for Product and Tech Stack knowledge bases.
/// </summary>
public class KeywordQuestionRouter : IQuestionRouter
{
    private static readonly HashSet<string> ProductKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLO", "CDO", "compliance", "waterfall", "trading",
        "portfolio management", "tranches", "overcollateralization", "reporting"
    };

    private static readonly HashSet<string> TechStackKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "React", "micro-frontends", ".NET", "microservices", "gRPC",
        "Kubernetes", "CI/CD", "Docker", "deployment pipelines"
    };

    public QuestionClassification Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return QuestionClassification.Both;

        var matchesProduct = ProductKeywords.Any(kw =>
            question.Contains(kw, StringComparison.OrdinalIgnoreCase));
        var matchesTechStack = TechStackKeywords.Any(kw =>
            question.Contains(kw, StringComparison.OrdinalIgnoreCase));

        return (matchesProduct, matchesTechStack) switch
        {
            (true, true) => QuestionClassification.Both,
            (true, false) => QuestionClassification.Product,
            (false, true) => QuestionClassification.TechStack,
            (false, false) => QuestionClassification.Both
        };
    }
}
