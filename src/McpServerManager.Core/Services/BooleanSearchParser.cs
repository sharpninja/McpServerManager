using System;

namespace McpServerManager.Core.Services;

/// <summary>
/// Parses boolean search expressions with || (OR), &amp;&amp; (AND), ! (NOT), and parentheses.
/// Returns a predicate that tests whether a given text matches the expression.
/// When no operators are present, space-separated terms are treated as an AND chain.
/// </summary>
public static class BooleanSearchParser
{
    public static Func<string, bool> Parse(string query)
        => global::McpServer.Cqrs.Search.BooleanSearchParser.Parse(query);
}
