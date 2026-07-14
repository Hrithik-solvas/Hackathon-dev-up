using CodeCompass.Chunking;
using FluentAssertions;

namespace CodeCompass.Tests.Unit.Chunking;

public class TokenCounterTests
{
    [Fact]
    public void CountTokens_WithSimpleText_ReturnsCorrectCount()
    {
        var result = TokenCounter.CountTokens("hello world foo bar");

        result.Should().Be(4);
    }

    [Fact]
    public void CountTokens_WithNull_ReturnsZero()
    {
        var result = TokenCounter.CountTokens(null!);

        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithEmptyString_ReturnsZero()
    {
        var result = TokenCounter.CountTokens(string.Empty);

        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithWhitespaceOnly_ReturnsZero()
    {
        var result = TokenCounter.CountTokens("   \t  \n  ");

        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithSingleWord_ReturnsOne()
    {
        var result = TokenCounter.CountTokens("hello");

        result.Should().Be(1);
    }

    [Fact]
    public void CountTokens_WithMultipleWhitespaceSeparators_CountsCorrectly()
    {
        var result = TokenCounter.CountTokens("hello   world\tfoo\nbar");

        result.Should().Be(4);
    }

    [Fact]
    public void CountTokens_WithLeadingAndTrailingWhitespace_IgnoresThem()
    {
        var result = TokenCounter.CountTokens("  hello world  ");

        result.Should().Be(2);
    }

    [Fact]
    public void Tokenize_WithSimpleText_ReturnsTokens()
    {
        var result = TokenCounter.Tokenize("hello world foo");

        result.Should().BeEquivalentTo(new[] { "hello", "world", "foo" });
    }

    [Fact]
    public void Tokenize_WithNull_ReturnsEmptyList()
    {
        var result = TokenCounter.Tokenize(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_WithEmptyString_ReturnsEmptyList()
    {
        var result = TokenCounter.Tokenize(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_WithWhitespaceOnly_ReturnsEmptyList()
    {
        var result = TokenCounter.Tokenize("   \t  \n  ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_WithMultipleWhitespaceSeparators_SplitsCorrectly()
    {
        var result = TokenCounter.Tokenize("hello   world\tfoo\nbar");

        result.Should().BeEquivalentTo(new[] { "hello", "world", "foo", "bar" });
    }

    [Fact]
    public void Tokenize_PreservesOrder()
    {
        var result = TokenCounter.Tokenize("zebra apple mango banana");

        result.Should().ContainInOrder("zebra", "apple", "mango", "banana");
    }

    [Fact]
    public void CountTokens_MatchesTokenizeCount()
    {
        var text = "The quick brown fox jumps over the lazy dog";

        var countResult = TokenCounter.CountTokens(text);
        var tokenizeResult = TokenCounter.Tokenize(text);

        countResult.Should().Be(tokenizeResult.Count);
    }

    [Fact]
    public void Tokenize_WithCodeContent_HandlesSpecialCharacters()
    {
        var result = TokenCounter.Tokenize("public void Method() { return; }");

        result.Should().HaveCount(6);
        result.Should().ContainInOrder("public", "void", "Method()", "{", "return;", "}");
    }

    [Fact]
    public void CountTokens_WithCodeContent_CountsCorrectly()
    {
        var result = TokenCounter.CountTokens("public void Method() { return; }");

        result.Should().Be(6);
    }
}
