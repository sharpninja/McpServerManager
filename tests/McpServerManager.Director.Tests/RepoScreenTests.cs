using McpServerManager.Director.Screens;
using McpServerManager.UI.Core.Messages;

namespace McpServerManager.Director.Tests;

public sealed class RepoScreenTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData("/")]
    public void TryGetParentPath_RootInputs_ReturnsFalse(string? path)
    {
        var hasParent = RepoScreen.TryGetParentPath(path, out var parentPath);

        Assert.False(hasParent);
        Assert.Equal(string.Empty, parentPath);
    }

    [Theory]
    [InlineData("src", "")]
    [InlineData("src/screens", "src")]
    [InlineData("./src/screens/", "src")]
    [InlineData("src\\screens\\repo", "src/screens")]
    public void TryGetParentPath_NonRootInputs_ReturnsExpectedParent(string path, string expectedParent)
    {
        var hasParent = RepoScreen.TryGetParentPath(path, out var parentPath);

        Assert.True(hasParent);
        Assert.Equal(expectedParent, parentPath);
    }

    [Fact]
    public void BuildEntriesForDisplay_RootPath_DoesNotAddParentNode()
    {
        var entries = new[]
        {
            new RepoEntrySummary("docs", true),
            new RepoEntrySummary("README.md", false),
        };

        var displayEntries = RepoScreen.BuildEntriesForDisplay(string.Empty, entries);

        Assert.Equal(2, displayEntries.Count);
        Assert.Equal("docs", displayEntries[0].Name);
        Assert.Equal("README.md", displayEntries[1].Name);
    }

    [Fact]
    public void BuildEntriesForDisplay_NonRootPath_PrependsSingleParentNode()
    {
        var entries = new[]
        {
            new RepoEntrySummary("..", true),
            new RepoEntrySummary("scripts", true),
            new RepoEntrySummary("appsettings.json", false),
        };

        var displayEntries = RepoScreen.BuildEntriesForDisplay("src/McpServerManager.Director", entries);

        Assert.Equal(3, displayEntries.Count);
        Assert.Equal("..", displayEntries[0].Name);
        Assert.True(displayEntries[0].IsDirectory);
        Assert.Equal("scripts", displayEntries[1].Name);
        Assert.Equal("appsettings.json", displayEntries[2].Name);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(".", "")]
    [InlineData("./src/screens/", "src/screens")]
    [InlineData("src\\screens\\repo", "src/screens/repo")]
    public void NormalizeListPath_NormalizesAsExpected(string? input, string expected)
    {
        var normalized = RepoScreen.NormalizeListPath(input);

        Assert.Equal(expected, normalized);
    }
}
