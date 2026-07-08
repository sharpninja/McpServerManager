using System;
using System.Linq;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

/// <summary>
/// PLAN-C2-TODO-FAMILY-001 / TR-HANDLER-EXTRACTION: verifies the line/task parse and
/// format helpers extracted from <c>TodoDetailViewModel</c> into the shared
/// <see cref="TodoMarkdownSerializer"/> (TR-MCP-DRY-001). Fixtures are literal strings;
/// validates FR-VM-HANDLER-MUTATION (logic lives outside the ViewModel).
/// </summary>
public sealed class TodoMarkdownParseTests
{
    /// <summary>Blank/whitespace input yields null (no empty collections leak into commands).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void ParseLines_BlankInput_ReturnsNull(string? input)
        => Assert.Null(TodoMarkdownSerializer.ParseLines(input));

    /// <summary>Non-empty lines are trimmed, empty lines dropped, order preserved.</summary>
    [Fact]
    public void ParseLines_TrimsAndDropsEmpty()
    {
        var result = TodoMarkdownSerializer.ParseLines("  FR-1 \r\n\n  FR-2\n");
        Assert.NotNull(result);
        Assert.Equal(new[] { "FR-1", "FR-2" }, result);
    }

    /// <summary>Blank input yields null tasks.</summary>
    [Fact]
    public void ParseTasks_BlankInput_ReturnsNull()
        => Assert.Null(TodoMarkdownSerializer.ParseTasks("   "));

    /// <summary>Checkbox and dash prefixes parse into done/undone task items.</summary>
    [Fact]
    public void ParseTasks_ParsesCheckboxAndDashPrefixes()
    {
        var result = TodoMarkdownSerializer.ParseTasks("[x] done one\n[ ] open two\n- bare three");
        Assert.NotNull(result);
        Assert.Collection(result!,
            t => { Assert.Equal("done one", t.Task); Assert.True(t.Done); },
            t => { Assert.Equal("open two", t.Task); Assert.False(t.Done); },
            t => { Assert.Equal("bare three", t.Task); Assert.False(t.Done); });
    }

    /// <summary>FormatLines round-trips with ParseLines.</summary>
    [Fact]
    public void FormatLines_RoundTripsWithParseLines()
    {
        var input = new[] { "A", "B", "C" };
        var text = TodoMarkdownSerializer.FormatLines(input);
        Assert.Equal(input, TodoMarkdownSerializer.ParseLines(text));
    }

    /// <summary>Empty list formats to null.</summary>
    [Fact]
    public void FormatLines_Empty_ReturnsNull()
        => Assert.Null(TodoMarkdownSerializer.FormatLines(Array.Empty<string>()));

    /// <summary>FormatTasks emits checkbox syntax that ParseTasks reads back.</summary>
    [Fact]
    public void FormatTasks_RoundTripsWithParseTasks()
    {
        var input = new[] { new TodoTaskDetail("alpha", true), new TodoTaskDetail("beta", false) };
        var text = TodoMarkdownSerializer.FormatTasks(input);
        var parsed = TodoMarkdownSerializer.ParseTasks(text);
        Assert.NotNull(parsed);
        Assert.Equal(input.Select(t => (t.Task, t.Done)), parsed!.Select(t => (t.Task, t.Done)));
    }
}
