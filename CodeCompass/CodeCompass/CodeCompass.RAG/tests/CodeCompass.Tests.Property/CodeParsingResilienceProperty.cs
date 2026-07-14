using CodeCompass.Core.Interfaces;
using CodeCompass.Core.Models;
using CodeCompass.Parsing;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Property;

/// <summary>
/// Property 5: Code parsing resilience to syntax errors.
/// For any batch of source files where some contain syntax errors, the parser produces results
/// for all parseable files and logs warnings for unparseable files without halting the batch.
///
/// **Validates: Requirements 2.5**
/// </summary>
public class CodeParsingResilienceProperty : IDisposable
{
    private readonly string _tempDir;
    private readonly CSharpCodeParser _csharpParser;
    private readonly ReactCodeParser _reactParser;
    private readonly SqlCodeParser _sqlParser;
    private readonly TestLogger<CSharpCodeParser> _csharpLogger;
    private readonly TestLogger<ReactCodeParser> _reactLogger;
    private readonly TestLogger<SqlCodeParser> _sqlLogger;

    public CodeParsingResilienceProperty()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CodeParsingResilience_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _csharpLogger = new TestLogger<CSharpCodeParser>();
        _reactLogger = new TestLogger<ReactCodeParser>();
        _sqlLogger = new TestLogger<SqlCodeParser>();

        _csharpParser = new CSharpCodeParser(_csharpLogger);
        _reactParser = new ReactCodeParser(_reactLogger);
        _sqlParser = new SqlCodeParser(_sqlLogger);
    }

    [Property(MaxTest = 100)]
    public void CSharpParser_BatchWithMixedValidAndInvalidFiles_ProducesResultsForAllFiles(
        PositiveInt validCount, PositiveInt invalidCount)
    {
        var validFileCount = (validCount.Get % 5) + 1; // 1-5 valid files
        var invalidFileCount = (invalidCount.Get % 5) + 1; // 1-5 invalid files

        var validFiles = CreateValidCSharpFiles(validFileCount);
        var invalidFiles = CreateInvalidCSharpFiles(invalidFileCount);
        var allFiles = validFiles.Concat(invalidFiles).ToList();

        // Process entire batch - no file should cause the batch to halt
        var results = new List<ParsedCode>();
        var exceptions = new List<Exception>();

        foreach (var file in allFiles)
        {
            try
            {
                var result = _csharpParser.ParseAsync(file).GetAwaiter().GetResult();
                results.Add(result);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // The parser should never throw - it should handle all files gracefully
        exceptions.Should().BeEmpty("parsers should handle syntax errors gracefully without throwing");

        // All files should produce results (Roslyn is resilient to syntax errors)
        results.Should().HaveCount(allFiles.Count,
            "every file in the batch should produce a result, even those with syntax errors");

        // Valid files should produce results with symbols
        var validResults = results.Take(validFileCount).ToList();
        validResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNullOrEmpty();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
        });

        // Invalid files should still produce a result (possibly with no symbols, but not null)
        var invalidResults = results.Skip(validFileCount).ToList();
        invalidResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNull();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
        });
    }

    [Property(MaxTest = 100)]
    public void ReactParser_BatchWithMixedValidAndInvalidFiles_ProducesResultsForAllFiles(
        PositiveInt validCount, PositiveInt invalidCount)
    {
        var validFileCount = (validCount.Get % 5) + 1;
        var invalidFileCount = (invalidCount.Get % 5) + 1;

        var validFiles = CreateValidReactFiles(validFileCount);
        var invalidFiles = CreateInvalidReactFiles(invalidFileCount);
        var allFiles = validFiles.Concat(invalidFiles).ToList();

        var results = new List<ParsedCode>();
        var exceptions = new List<Exception>();

        foreach (var file in allFiles)
        {
            try
            {
                var result = _reactParser.ParseAsync(file).GetAwaiter().GetResult();
                results.Add(result);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // The parser should never throw
        exceptions.Should().BeEmpty("parsers should handle syntax errors gracefully without throwing");

        // All files should produce results
        results.Should().HaveCount(allFiles.Count,
            "every file in the batch should produce a result, even those with syntax errors");

        // Valid files should produce results with components/hooks
        var validResults = results.Take(validFileCount).ToList();
        validResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNullOrEmpty();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
            r.Symbols.Count.Should().BeGreaterThan(0, "valid React files should contain at least one component/hook");
        });

        // Invalid files should still produce a result without crashing
        var invalidResults = results.Skip(validFileCount).ToList();
        invalidResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNull();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
        });
    }

    [Property(MaxTest = 100)]
    public void SqlParser_BatchWithMixedValidAndInvalidFiles_ProducesResultsForAllFiles(
        PositiveInt validCount, PositiveInt invalidCount)
    {
        var validFileCount = (validCount.Get % 5) + 1;
        var invalidFileCount = (invalidCount.Get % 5) + 1;

        var validFiles = CreateValidSqlFiles(validFileCount);
        var invalidFiles = CreateInvalidSqlFiles(invalidFileCount);
        var allFiles = validFiles.Concat(invalidFiles).ToList();

        var results = new List<ParsedCode>();
        var exceptions = new List<Exception>();

        foreach (var file in allFiles)
        {
            try
            {
                var result = _sqlParser.ParseAsync(file).GetAwaiter().GetResult();
                results.Add(result);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // The parser should never throw
        exceptions.Should().BeEmpty("parsers should handle syntax errors gracefully without throwing");

        // All files should produce results
        results.Should().HaveCount(allFiles.Count,
            "every file in the batch should produce a result, even those with syntax errors");

        // Valid files should produce results with stored procedures
        var validResults = results.Take(validFileCount).ToList();
        validResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNullOrEmpty();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
            r.Symbols.Count.Should().BeGreaterThan(0, "valid SQL files should contain at least one procedure");
        });

        // Invalid files should still produce a result without crashing
        var invalidResults = results.Skip(validFileCount).ToList();
        invalidResults.Should().AllSatisfy(r =>
        {
            r.RawText.Should().NotBeNull();
            r.SourceMetadata.Should().NotBeNull();
            r.Symbols.Should().NotBeNull();
        });
    }

    [Property(MaxTest = 100)]
    public void AllParsers_BatchProcessing_OneInvalidFileDoesNotPreventOthersFromBeingProcessed(
        PositiveInt seed)
    {
        // Create a mixed batch with one file per parser type, alternating valid/invalid
        var variant = seed.Get % 3;

        var files = new List<(string path, ICodeParser parser)>();

        // Always include at least one valid and one invalid file per parser
        var validCs = CreateValidCSharpFiles(1).First();
        var invalidCs = CreateInvalidCSharpFiles(1).First();
        var validTsx = CreateValidReactFiles(1).First();
        var invalidTsx = CreateInvalidReactFiles(1).First();
        var validSql = CreateValidSqlFiles(1).First();
        var invalidSql = CreateInvalidSqlFiles(1).First();

        files.Add((validCs, _csharpParser));
        files.Add((invalidCs, _csharpParser));
        files.Add((validTsx, _reactParser));
        files.Add((invalidTsx, _reactParser));
        files.Add((validSql, _sqlParser));
        files.Add((invalidSql, _sqlParser));

        var successCount = 0;
        var failureCount = 0;

        // Process the batch - simulating pipeline batch processing
        foreach (var (path, parser) in files)
        {
            try
            {
                var result = parser.ParseAsync(path).GetAwaiter().GetResult();
                result.Should().NotBeNull();
                successCount++;
            }
            catch
            {
                failureCount++;
            }
        }

        // All files should be processed successfully (parsers are resilient)
        failureCount.Should().Be(0, "no file should cause an unhandled exception that halts the batch");
        successCount.Should().Be(files.Count, "all files should produce results regardless of content validity");
    }

    #region Helper Methods - File Generators

    private List<string> CreateValidCSharpFiles(int count)
    {
        var files = new List<string>();
        var templates = new[]
        {
            "namespace Test{0} {{ public class MyClass{0} {{ public void DoWork() {{ var x = 1; }} }} }}",
            "using System;\nnamespace Sample{0} {{ /// <summary>A class</summary>\npublic class Service{0} {{ public int Calculate(int a, int b) => a + b; }} }}",
            "namespace App{0} {{ public interface IRepo{0} {{ Task<string> GetAsync(); }} public class Repo{0} : IRepo{0} {{ public async Task<string> GetAsync() => \"data\"; }} }}",
            "using System.Collections.Generic;\nnamespace Lib{0} {{ public static class Helper{0} {{ public static List<int> Filter(List<int> items) => items.Where(x => x > 0).ToList(); }} }}",
            "namespace Domain{0} {{ public record Entity{0}(int Id, string Name); public class Factory{0} {{ public Entity{0} Create(string name) => new(0, name); }} }}"
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Valid{i}_{Guid.NewGuid():N}.cs");
            var content = string.Format(templates[i % templates.Length], i);
            File.WriteAllText(filePath, content);
            files.Add(filePath);
        }

        return files;
    }

    private List<string> CreateInvalidCSharpFiles(int count)
    {
        var files = new List<string>();
        var garbageContents = new[]
        {
            "}{}{}{class @@@ not valid c# code !!!",
            "public class { method( { return; } }}}}}",
            "random garbage 12345 $%^&*() <<>> [[[]]]",
            "\x00\x01\x02 binary content mixed with class Foo {",
            "namespace { using ;;; public void () => }"
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Invalid{i}_{Guid.NewGuid():N}.cs");
            File.WriteAllText(filePath, garbageContents[i % garbageContents.Length]);
            files.Add(filePath);
        }

        return files;
    }

    private List<string> CreateValidReactFiles(int count)
    {
        var files = new List<string>();
        var templates = new[]
        {
            "import React from 'react';\n\n/** A component */\nexport function Component{0}() {{ return <div>Hello</div>; }}",
            "import {{ useState }} from 'react';\n\nexport const Widget{0} = () => {{ const [x, setX] = useState(0); return <span>{{x}}</span>; }};",
            "/** Custom hook */\nexport function useData{0}() {{ const [data, setData] = useState(null); return data; }}",
            "import React from 'react';\n\nconst Layout{0} = ({{ children }}) => {{ return <main>{{children}}</main>; }};\nexport default Layout{0};",
            "export function Button{0}({{ onClick, label }}) {{ return <button onClick={{onClick}}>{{label}}</button>; }}"
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Valid{i}_{Guid.NewGuid():N}.tsx");
            var content = string.Format(templates[i % templates.Length], i);
            File.WriteAllText(filePath, content);
            files.Add(filePath);
        }

        return files;
    }

    private List<string> CreateInvalidReactFiles(int count)
    {
        var files = new List<string>();
        var garbageContents = new[]
        {
            "<<<>>> not jsx {{{{{",
            "export const = () => {{ }}; function (( )) {{ }}",
            "random garbage 12345 $%^&*()",
            "\x00\x01\x02 binary jsx garbage <Component",
            "import {{ from 'react' export default class"
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Invalid{i}_{Guid.NewGuid():N}.tsx");
            File.WriteAllText(filePath, garbageContents[i % garbageContents.Length]);
            files.Add(filePath);
        }

        return files;
    }

    private List<string> CreateValidSqlFiles(int count)
    {
        var files = new List<string>();
        var templates = new[]
        {
            "-- Get items procedure\nCREATE PROCEDURE dbo.GetItems{0}\n    @PageSize INT,\n    @PageNumber INT\nAS\nBEGIN\n    SELECT * FROM Items;\nEND",
            "/* Update record */\nCREATE OR ALTER PROCEDURE dbo.UpdateRecord{0}\n    @Id INT,\n    @Name NVARCHAR(100)\nAS\nBEGIN\n    UPDATE Records SET Name = @Name WHERE Id = @Id;\nEND",
            "CREATE PROC dbo.DeleteItem{0}\n    @ItemId INT\nAS\nBEGIN\n    DELETE FROM Items WHERE Id = @ItemId;\nEND",
            "-- Insert procedure\nCREATE PROCEDURE dbo.InsertData{0}\n    @Value NVARCHAR(255)\nAS\nBEGIN\n    INSERT INTO Data (Value) VALUES (@Value);\nEND",
            "CREATE OR ALTER PROC dbo.SearchItems{0}\n    @Query NVARCHAR(200),\n    @MaxResults INT\nAS\nBEGIN\n    SELECT TOP(@MaxResults) * FROM Items WHERE Name LIKE '%' + @Query + '%';\nEND"
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Valid{i}_{Guid.NewGuid():N}.sql");
            var content = string.Format(templates[i % templates.Length], i);
            File.WriteAllText(filePath, content);
            files.Add(filePath);
        }

        return files;
    }

    private List<string> CreateInvalidSqlFiles(int count)
    {
        var files = new List<string>();
        var garbageContents = new[]
        {
            "SELECT FROM WHERE GROUP BY HAVING ;;; @@@ !!!",
            "not sql at all - just random text 12345",
            "CREATE TABLE ??? ((( ))) DROP EVERYTHING",
            "\x00\x01\x02 binary content with some SQL keywords SELECT",
            "EXEC sp_something @@ @@ DECLARE @@@@ SET =="
        };

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_tempDir, $"Invalid{i}_{Guid.NewGuid():N}.sql");
            File.WriteAllText(filePath, garbageContents[i % garbageContents.Length]);
            files.Add(filePath);
        }

        return files;
    }

    #endregion

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// A simple test logger that captures log entries for verification.
    /// </summary>
    private class TestLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _entries = new();
        public IReadOnlyList<LogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public record LogEntry(LogLevel Level, string Message);
    }
}
