using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Parsing;

/// <summary>
/// Parses C# source files using Roslyn syntax analysis, extracting classes,
/// methods, and XML documentation comments.
/// </summary>
public class CSharpCodeParser : ICodeParser
{
    private readonly ILogger<CSharpCodeParser> _logger;

    public CSharpCodeParser(ILogger<CSharpCodeParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanParse(string fileExtension)
    {
        return string.Equals(fileExtension, ".cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedCode> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Parsing C# file: {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var syntaxTree = CSharpSyntaxTree.ParseText(content, cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        var symbols = ExtractSymbols(root);
        var documentationComments = ExtractDocumentationComments(root);

        var fileInfo = new FileInfo(filePath);
        var sourceMetadata = new SourceFileMetadata(
            FilePath: fileInfo.FullName,
            FileName: fileInfo.Name,
            FileExtension: fileInfo.Extension,
            LastModified: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));

        _logger.LogDebug(
            "Parsed C# file {FilePath}: {SymbolCount} symbols, {CommentCount} documentation comments found",
            filePath, symbols.Count, documentationComments.Count);

        return new ParsedCode(content, symbols, documentationComments, sourceMetadata);
    }

    private static List<CodeSymbol> ExtractSymbols(SyntaxNode root)
    {
        var symbols = new List<CodeSymbol>();

        foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var className = classDeclaration.Identifier.Text;
            var parentNamespace = GetParentNamespace(classDeclaration);

            symbols.Add(new CodeSymbol(className, CodeSymbolKind.Class, parentNamespace));

            foreach (var methodDeclaration in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodName = methodDeclaration.Identifier.Text;
                symbols.Add(new CodeSymbol(methodName, CodeSymbolKind.Method, className));
            }
        }

        return symbols;
    }

    private static List<string> ExtractDocumentationComments(SyntaxNode root)
    {
        var comments = new List<string>();

        foreach (var node in root.DescendantNodes())
        {
            if (node is not (ClassDeclarationSyntax or MethodDeclarationSyntax))
            {
                continue;
            }

            var trivia = node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .ToList();

            foreach (var docTrivia in trivia)
            {
                var structure = docTrivia.GetStructure();
                if (structure is DocumentationCommentTriviaSyntax docComment)
                {
                    var summaryElement = docComment.Content
                        .OfType<XmlElementSyntax>()
                        .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

                    if (summaryElement != null)
                    {
                        var summaryText = summaryElement.Content.ToString()
                            .Replace("///", "")
                            .Trim();

                        if (!string.IsNullOrWhiteSpace(summaryText))
                        {
                            comments.Add(summaryText);
                        }
                    }
                }
            }
        }

        return comments;
    }

    private static string? GetParentNamespace(ClassDeclarationSyntax classDeclaration)
    {
        var parent = classDeclaration.Parent;

        while (parent != null)
        {
            if (parent is NamespaceDeclarationSyntax namespaceDecl)
            {
                return namespaceDecl.Name.ToString();
            }

            if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
            {
                return fileScopedNamespace.Name.ToString();
            }

            parent = parent.Parent;
        }

        return null;
    }
}
