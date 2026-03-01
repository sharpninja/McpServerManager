using System.Text.RegularExpressions;

namespace McpServerManager.Core.Utilities;

/// <summary>
/// Text transformation utilities for processing AI response text from SSE streams.
/// </summary>
public static class TextTransformations
{
    // Matches bare HTTP/HTTPS URIs not already wrapped in a markdown link [text](url).
    // Lookbehind excludes URIs already preceded by ( (inside markdown link target) or [ (inside label).
    private static readonly Regex BareUriPattern = new(
        @"(?<!\(|\[)https?://[^\s)\]`""<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Converts bare HTTP/HTTPS URIs in <paramref name="text"/> to markdown hyperlinks
    /// of the form <c>[uri](uri)</c>. URIs already inside a markdown link are left unchanged.
    /// Only intended for accumulated response text from SSE streams.
    /// </summary>
    /// <param name="text">The accumulated SSE response text to transform.</param>
    /// <returns>The text with bare URIs replaced by markdown hyperlinks.</returns>
    public static string ConvertBareUrisToMarkdownLinks(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return BareUriPattern.Replace(text, m => $"[{m.Value}]({m.Value})");
    }
}
