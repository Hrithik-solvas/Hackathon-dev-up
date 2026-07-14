using CodeCompass.Chunking;
using CodeCompass.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Chunking;

public class ChunkingServiceCodeTests
{
    private readonly ChunkingService _service;
    private readonly SourceFileMetadata _testMetadata;

    public ChunkingServiceCodeTests()
    {
        var logger = NullLogger<ChunkingService>.Instance;
        _service = new ChunkingService(logger);
        _testMetadata = new SourceFileMetadata(
            FilePath: "/repo/src/MyClass.cs",
            FileName: "MyClass.cs",
            FileExtension: ".cs",
            LastModified: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ChunkCode_EmptyText_ReturnsNoChunks()
    {
        var code = new ParsedCode("", new List<CodeSymbol>(), new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkCode_WhitespaceOnlyText_ReturnsNoChunks()
    {
        var code = new ParsedCode("   \n\n   ", new List<CodeSymbol>(), new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkCode_SingleMethodWithinMax_ReturnsSingleChunk()
    {
        var text = "public void DoSomething()\n{\n    Console.WriteLine(\"hello\");\n}";
        var symbols = new List<CodeSymbol>
        {
            new("DoSomething", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0);
        result[0].Metadata.SourceFilePath.Should().Be("/repo/src/MyClass.cs");
        result[0].Metadata.ContentType.Should().Be("code");
    }

    [Fact]
    public void ChunkCode_ContentType_IsCode()
    {
        var text = "public class Foo\n{\n    public void Bar() { }\n}";
        var symbols = new List<CodeSymbol>
        {
            new("Foo", CodeSymbolKind.Class, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        result.Should().NotBeEmpty();
        result.All(c => c.Metadata.ContentType == "code").Should().BeTrue();
    }

    [Fact]
    public void ChunkCode_ReferencesSourceFilePath()
    {
        var text = "public class Foo { }";
        var symbols = new List<CodeSymbol>
        {
            new("Foo", CodeSymbolKind.Class, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        result.Should().HaveCount(1);
        result[0].Metadata.SourceFilePath.Should().Be("/repo/src/MyClass.cs");
    }

    [Fact]
    public void ChunkCode_SplitsAtClassBoundaries()
    {
        var text = @"public class ClassA
{
    public void MethodA()
    {
        // " + string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}")) + @"
    }
}

public class ClassB
{
    public void MethodB()
    {
        // " + string.Join(" ", Enumerable.Range(1, 200).Select(i => $"other{i}")) + @"
    }
}";
        var symbols = new List<CodeSymbol>
        {
            new("ClassA", CodeSymbolKind.Class, null),
            new("ClassB", CodeSymbolKind.Class, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 250, MinTokens: 20, OverlapTokens: 30);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        // First chunk should contain ClassA content
        result[0].Text.Should().Contain("ClassA");
    }

    [Fact]
    public void ChunkCode_SplitsAtMethodBoundaries()
    {
        var method1Body = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"stmt{i}"));
        var method2Body = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"expr{i}"));
        var text = $"public void MethodOne()\n{{\n    // {method1Body}\n}}\n\npublic void MethodTwo()\n{{\n    // {method2Body}\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("MethodOne", CodeSymbolKind.Method, "MyClass"),
            new("MethodTwo", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 250, MinTokens: 20, OverlapTokens: 30);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        result[0].Text.Should().Contain("MethodOne");
    }

    [Fact]
    public void ChunkCode_SequentialZeroBasedIndices()
    {
        // Create code with multiple methods that will produce multiple chunks
        var methods = Enumerable.Range(1, 5)
            .Select(i => $"public void Method{i}()\n{{\n    // {string.Join(" ", Enumerable.Range(1, 100).Select(j => $"w{i}_{j}"))}\n}}")
            .ToList();
        var text = string.Join("\n\n", methods);
        var symbols = Enumerable.Range(1, 5)
            .Select(i => new CodeSymbol($"Method{i}", CodeSymbolKind.Method, "MyClass"))
            .ToList();
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 150, MinTokens: 20, OverlapTokens: 20);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
            result[i].Metadata.ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkCode_AppliesOverlap_AdjacentChunksShareTokens()
    {
        var method1Body = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"alpha{i}"));
        var method2Body = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"beta{i}"));
        var text = $"public void First()\n{{\n    // {method1Body}\n}}\n\npublic void Second()\n{{\n    // {method2Body}\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("First", CodeSymbolKind.Method, "MyClass"),
            new("Second", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 250, MinTokens: 20, OverlapTokens: 50);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        // The second chunk should start with tokens from the end of the first chunk (overlap)
        var firstTokens = result[0].Text.Split(' ');
        var secondTokens = result[1].Text.Split(' ');
        var overlapFromFirst = firstTokens.TakeLast(50).ToArray();
        var startOfSecond = secondTokens.Take(50).ToArray();
        overlapFromFirst.Should().BeEquivalentTo(startOfSecond);
    }

    [Fact]
    public void ChunkCode_NoSymbols_FallsBackToBlankLineSplitting()
    {
        var block1 = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"line{i}"));
        var block2 = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"code{i}"));
        var text = $"{block1}\n\n{block2}";
        var code = new ParsedCode(text, new List<CodeSymbol>(), new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 250, MinTokens: 20, OverlapTokens: 30);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkCode_SmallTrailingContent_MergedWithPrevious()
    {
        var methodBody = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"word{i}"));
        var text = $"public void BigMethod()\n{{\n    // {methodBody}\n}}\n\n// end";
        var symbols = new List<CodeSymbol>
        {
            new("BigMethod", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);

        var result = _service.ChunkCode(code, options);

        // The small trailing "// end" should be merged
        result.Should().HaveCount(1);
        result[0].Text.Should().Contain("end");
    }

    [Fact]
    public void ChunkCode_DefaultOptions_UsesCorrectDefaults()
    {
        var text = "public void Foo() { return; }";
        var symbols = new List<CodeSymbol>
        {
            new("Foo", CodeSymbolKind.Method, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);

        // Should use defaults: MaxTokens=512, MinTokens=50, OverlapTokens=50
        var result = _service.ChunkCode(code);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ChunkCode_StoredProcedure_SplitsAtProcBoundaries()
    {
        var procBody1 = string.Join(" ", Enumerable.Range(1, 150).Select(i => $"SELECT{i}"));
        var procBody2 = string.Join(" ", Enumerable.Range(1, 150).Select(i => $"INSERT{i}"));
        var text = $"CREATE PROCEDURE GetUsers\nAS\nBEGIN\n    {procBody1}\nEND\n\nCREATE PROCEDURE AddUser\nAS\nBEGIN\n    {procBody2}\nEND";
        var symbols = new List<CodeSymbol>
        {
            new("GetUsers", CodeSymbolKind.StoredProcedure, null),
            new("AddUser", CodeSymbolKind.StoredProcedure, null)
        };
        var sqlMetadata = new SourceFileMetadata(
            FilePath: "/repo/sql/procedures.sql",
            FileName: "procedures.sql",
            FileExtension: ".sql",
            LastModified: DateTimeOffset.UtcNow);
        var code = new ParsedCode(text, symbols, new List<string>(), sqlMetadata);
        var options = new ChunkingOptions(MaxTokens: 200, MinTokens: 20, OverlapTokens: 20);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        result[0].Text.Should().Contain("GetUsers");
        result[0].Metadata.Language.Should().Be("sql");
    }

    [Fact]
    public void ChunkCode_ReactComponent_SplitsAtComponentBoundaries()
    {
        var compBody1 = string.Join(" ", Enumerable.Range(1, 150).Select(i => $"div{i}"));
        var compBody2 = string.Join(" ", Enumerable.Range(1, 150).Select(i => $"span{i}"));
        var text = $"export function Header() {{\n    return ({compBody1});\n}}\n\nexport function Footer() {{\n    return ({compBody2});\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("Header", CodeSymbolKind.Component, null),
            new("Footer", CodeSymbolKind.Component, null)
        };
        var jsxMetadata = new SourceFileMetadata(
            FilePath: "/repo/src/components/Layout.tsx",
            FileName: "Layout.tsx",
            FileExtension: ".tsx",
            LastModified: DateTimeOffset.UtcNow);
        var code = new ParsedCode(text, symbols, new List<string>(), jsxMetadata);
        var options = new ChunkingOptions(MaxTokens: 200, MinTokens: 20, OverlapTokens: 20);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        result[0].Text.Should().Contain("Header");
        result[0].Metadata.Language.Should().Be("typescript");
    }

    [Fact]
    public void ChunkCode_SetsLanguageFromExtension()
    {
        var text = "public class Foo { }";
        var symbols = new List<CodeSymbol> { new("Foo", CodeSymbolKind.Class, null) };

        // C# file
        var csCode = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var csResult = _service.ChunkCode(csCode);
        csResult[0].Metadata.Language.Should().Be("csharp");

        // SQL file
        var sqlMetadata = new SourceFileMetadata("/repo/test.sql", "test.sql", ".sql", DateTimeOffset.UtcNow);
        var sqlCode = new ParsedCode("CREATE PROCEDURE Foo AS BEGIN END", symbols, new List<string>(), sqlMetadata);
        var sqlResult = _service.ChunkCode(sqlCode);
        sqlResult[0].Metadata.Language.Should().Be("sql");
    }

    [Fact]
    public void ChunkCode_MultipleUnitsWithinMax_ReturnsSingleChunk()
    {
        var text = "public void A()\n{\n    return;\n}\n\npublic void B()\n{\n    return;\n}";
        var symbols = new List<CodeSymbol>
        {
            new("A", CodeSymbolKind.Method, "MyClass"),
            new("B", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 512, MinTokens: 50, OverlapTokens: 50);

        var result = _service.ChunkCode(code, options);

        // Both methods are tiny, so they should fit in one chunk
        result.Should().HaveCount(1);
        result[0].Text.Should().Contain("A(");
        result[0].Text.Should().Contain("B(");
    }

    [Fact]
    public void ChunkCode_PreambleBeforeFirstSymbol_IncludedInChunks()
    {
        var text = "using System;\nusing System.Linq;\n\nnamespace MyApp\n{\n\npublic class Foo\n{\n    public void Bar() { }\n}\n}";
        var symbols = new List<CodeSymbol>
        {
            new("Foo", CodeSymbolKind.Class, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);

        var result = _service.ChunkCode(code);

        // The preamble (using statements) should be included in the output
        var allText = string.Join(" ", result.Select(c => c.Text));
        allText.Should().Contain("using");
        allText.Should().Contain("System");
    }

    [Fact]
    public void ChunkCode_OversizedUnit_SplitsAtStatementBoundaries()
    {
        // Create a method with many statements that together exceed max tokens
        var statements = Enumerable.Range(1, 50)
            .Select(i => $"    var variable{i} = someValue{i} + otherValue{i};")
            .ToList();
        var methodBody = string.Join("\n", statements);
        var text = $"public void BigMethod()\n{{\n{methodBody}\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("BigMethod", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkCode(code, options);

        // Should produce multiple chunks from the oversized unit
        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkCode_OversizedUnit_HasContextHeader_WithDeclarationSignature()
    {
        // Create a class with many statements that exceeds max tokens
        var statements = Enumerable.Range(1, 50)
            .Select(i => $"    var variable{i} = someValue{i} + otherValue{i};")
            .ToList();
        var methodBody = string.Join("\n", statements);
        var text = $"public class BigClass {{\n{methodBody}\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("BigClass", CodeSymbolKind.Class, null)
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkCode(code, options);

        // All sub-chunks should have context header set to the declaration signature
        result.Should().HaveCountGreaterThan(1);
        result.Should().AllSatisfy(c =>
        {
            c.ContextHeader.Should().NotBeNull();
            c.ContextHeader.Should().Be("public class BigClass {");
        });
    }

    [Fact]
    public void ChunkCode_OversizedUnit_SequentialIndices()
    {
        var statements = Enumerable.Range(1, 50)
            .Select(i => $"    var x{i} = {i};")
            .ToList();
        var methodBody = string.Join("\n", statements);
        var text = $"public void BigMethod()\n{{\n{methodBody}\n}}";
        var symbols = new List<CodeSymbol>
        {
            new("BigMethod", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 30, MinTokens: 5, OverlapTokens: 5);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
            result[i].Metadata.ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkCode_OversizedStoredProcedure_HasProcSignatureAsContextHeader()
    {
        var procStatements = Enumerable.Range(1, 50)
            .Select(i => $"    SELECT col{i} FROM table{i};")
            .ToList();
        var procBody = string.Join("\n", procStatements);
        var text = $"CREATE PROCEDURE GetAllData\nAS\nBEGIN\n{procBody}\nEND";
        var symbols = new List<CodeSymbol>
        {
            new("GetAllData", CodeSymbolKind.StoredProcedure, null)
        };
        var sqlMetadata = new SourceFileMetadata(
            FilePath: "/repo/sql/big_proc.sql",
            FileName: "big_proc.sql",
            FileExtension: ".sql",
            LastModified: DateTimeOffset.UtcNow);
        var code = new ParsedCode(text, symbols, new List<string>(), sqlMetadata);
        var options = new ChunkingOptions(MaxTokens: 50, MinTokens: 10, OverlapTokens: 10);

        var result = _service.ChunkCode(code, options);

        result.Should().HaveCountGreaterThan(1);
        result.Should().AllSatisfy(c =>
        {
            c.ContextHeader.Should().NotBeNull();
            c.ContextHeader.Should().Be("CREATE PROCEDURE GetAllData");
        });
    }

    [Fact]
    public void ChunkCode_MixedSizedUnits_OversizedHandledCorrectly()
    {
        // A small method, then an oversized method, then another small one
        var smallMethod1 = "public void SmallA()\n{\n    return;\n}";
        var bigStatements = Enumerable.Range(1, 50)
            .Select(i => $"    var x{i} = {i};")
            .ToList();
        var bigMethod = $"public void BigMethod()\n{{\n{string.Join("\n", bigStatements)}\n}}";
        var smallMethod2 = "public void SmallB()\n{\n    return;\n}";
        var text = $"{smallMethod1}\n\n{bigMethod}\n\n{smallMethod2}";
        var symbols = new List<CodeSymbol>
        {
            new("SmallA", CodeSymbolKind.Method, "MyClass"),
            new("BigMethod", CodeSymbolKind.Method, "MyClass"),
            new("SmallB", CodeSymbolKind.Method, "MyClass")
        };
        var code = new ParsedCode(text, symbols, new List<string>(), _testMetadata);
        var options = new ChunkingOptions(MaxTokens: 30, MinTokens: 5, OverlapTokens: 5);

        var result = _service.ChunkCode(code, options);

        // Should have multiple chunks, indices sequential
        result.Should().HaveCountGreaterThan(2);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void SplitAtStatementBoundaries_SplitsCorrectly()
    {
        var text = "var x = 1;\nvar y = 2;\nif (true) {\n    doSomething();\n}\nvar z = 3;";

        var result = ChunkingService.SplitAtStatementBoundaries(text);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ExtractDeclarationSignature_ReturnsFirstLine()
    {
        var unit = "public class MyClass {\n    public void Method() { }\n}";

        var result = ChunkingService.ExtractDeclarationSignature(unit);

        result.Should().Be("public class MyClass {");
    }

    [Fact]
    public void ExtractDeclarationSignature_NullForEmpty()
    {
        var result = ChunkingService.ExtractDeclarationSignature("");

        result.Should().BeNull();
    }
}
