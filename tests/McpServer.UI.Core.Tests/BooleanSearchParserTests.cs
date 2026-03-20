using McpServer.UI.Core.Services;
using Xunit;

namespace McpServer.UI.Core.Tests;

public sealed class BooleanSearchParserTests
{
    [Fact]
    public void Parse_EmptyQuery_MatchesAll()
    {
        var matcher = BooleanSearchParser.Parse("");

        Assert.True(matcher("anything"));
    }

    [Fact]
    public void Parse_NoOperators_TreatsTermsAsAnd()
    {
        var matcher = BooleanSearchParser.Parse("alpha beta");

        Assert.True(matcher("alpha beta release"));
        Assert.False(matcher("alpha release"));
    }

    [Fact]
    public void Parse_QuotedPhrase_MatchesWholePhrase()
    {
        var matcher = BooleanSearchParser.Parse("\"alpha beta\" && rollout");

        Assert.True(matcher("alpha beta rollout"));
        Assert.False(matcher("alpha release beta rollout"));
    }

    [Fact]
    public void Parse_OperatorsAndParentheses_RespectPrecedence()
    {
        var matcher = BooleanSearchParser.Parse("(alpha || beta) && !gamma");

        Assert.True(matcher("beta release"));
        Assert.False(matcher("beta gamma release"));
    }
}
