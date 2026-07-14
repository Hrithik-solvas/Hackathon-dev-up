namespace CodeCompass.Chunking;

/// <summary>
/// A simple, fast token counting utility using whitespace-based approximation.
/// Splits text on whitespace characters and counts non-empty parts.
/// Used by the ChunkingService to enforce token bounds on chunks.
/// </summary>
public static class TokenCounter
{
    /// <summary>
    /// Counts the number of tokens in the given text using whitespace-based splitting.
    /// Each non-empty segment separated by whitespace is counted as one token.
    /// </summary>
    /// <param name="text">The text to count tokens in.</param>
    /// <returns>The number of tokens, or 0 if the text is null or whitespace-only.</returns>
    public static int CountTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Tokenizes the given text by splitting on whitespace boundaries.
    /// Returns a list of non-empty token strings preserving their original order.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>A read-only list of tokens, or an empty list if the text is null or whitespace-only.</returns>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }
}
