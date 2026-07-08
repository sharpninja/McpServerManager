using McpServerManager.UI.Core.Services;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

/// <summary>
/// PLAN-REQSDESKTOP-001 / FR-REQS-CROSSLINK-001: verifies the back/forward navigation stack used by
/// the crosslinked requirement view. Fixtures are string ids.
/// </summary>
public sealed class NavigationStackServiceTests
{
    /// <summary>A fresh stack has no current and cannot navigate.</summary>
    [Fact]
    public void Empty_HasNoCurrentOrHistory()
    {
        var nav = new NavigationStackService<string>();
        Assert.Null(nav.Current);
        Assert.False(nav.CanGoBack);
        Assert.False(nav.CanGoForward);
    }

    /// <summary>Navigate sets current; a second navigate enables back.</summary>
    [Fact]
    public void Navigate_TracksCurrentAndBack()
    {
        var nav = new NavigationStackService<string>();
        nav.Navigate("A");
        Assert.Equal("A", nav.Current);
        Assert.False(nav.CanGoBack);

        nav.Navigate("B");
        Assert.Equal("B", nav.Current);
        Assert.True(nav.CanGoBack);
        Assert.False(nav.CanGoForward);
    }

    /// <summary>Back and Forward move through history without losing entries.</summary>
    [Fact]
    public void BackAndForward_TraverseHistory()
    {
        var nav = new NavigationStackService<string>();
        nav.Navigate("A");
        nav.Navigate("B");
        nav.Navigate("C");

        Assert.Equal("B", nav.Back());
        Assert.Equal("B", nav.Current);
        Assert.True(nav.CanGoForward);
        Assert.True(nav.CanGoBack);

        Assert.Equal("A", nav.Back());
        Assert.False(nav.CanGoBack);

        Assert.Equal("B", nav.Forward());
        Assert.Equal("C", nav.Forward());
        Assert.False(nav.CanGoForward);
    }

    /// <summary>Navigating after Back truncates the forward branch.</summary>
    [Fact]
    public void Navigate_AfterBack_TruncatesForward()
    {
        var nav = new NavigationStackService<string>();
        nav.Navigate("A");
        nav.Navigate("B");
        nav.Back(); // at A

        nav.Navigate("X");
        Assert.Equal("X", nav.Current);
        Assert.True(nav.CanGoBack);
        Assert.False(nav.CanGoForward); // B branch discarded
        Assert.Equal("A", nav.Back());
    }

    /// <summary>Back/Forward at the ends are no-ops that keep current.</summary>
    [Fact]
    public void BackForward_AtEnds_AreNoOps()
    {
        var nav = new NavigationStackService<string>();
        Assert.Equal(default, nav.Back());
        nav.Navigate("A");
        Assert.Equal("A", nav.Back());   // already at oldest
        Assert.Equal("A", nav.Forward()); // already at newest
        Assert.Equal("A", nav.Current);
    }
}
