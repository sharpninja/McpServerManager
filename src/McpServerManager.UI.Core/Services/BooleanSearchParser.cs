using System;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Parses boolean search expressions with || (OR), &amp;&amp; (AND), ! (NOT), and parentheses.
/// Returns a predicate that tests whether a given text matches the expression.
/// When no operators are present, space-separated terms are treated as an AND chain.
/// </summary>
public static class BooleanSearchParser
{
    /// <summary>
    /// Parses a boolean search query into a predicate that evaluates input text.
    /// </summary>
    /// <param name="query">Query text using <c>||</c>, <c>&amp;&amp;</c>, <c>!</c>, and parentheses.</param>
    /// <returns>A predicate that returns true when the text matches the query.</returns>
    public static Func<string, bool> Parse(string query)
        => global::McpServer.Cqrs.Search.BooleanSearchParser.Parse(query);
}
