using System.Linq;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

/// <summary>
/// PLAN-C2-TODO-FAMILY-001 / TR-HANDLER-EXTRACTION + FR-VM-HANDLER-MUTATION: verifies the
/// entry-building, filtering, and grouping logic extracted from <c>TodoListHostViewModel</c>
/// (ApplyFilters/MatchesTextFilter/BuildEntries/FormatPriority/PrioritySortKey) into the pure
/// <see cref="TodoListProjectionService"/>. Fixtures are literal <see cref="TodoListItem"/> lists.
/// </summary>
public sealed class TodoListProjectionServiceTests
{
    private static readonly ITodoListProjectionService Svc = new TodoListProjectionService();

    private static TodoListItem Item(string id, string priority, string title, bool done = false)
        => new(id, title, "sec", priority, done, null);

    /// <summary>Entries are sorted high->medium->low then by id, with display fields set.</summary>
    [Fact]
    public void BuildEntries_SortsByPriorityThenId_AndSetsDisplay()
    {
        var entries = Svc.BuildEntries(new[] { Item("T-2", "low", "B"), Item("T-1", "high", "A"), Item("T-3", "medium", "C") });
        Assert.Equal(new[] { "T-1", "T-3", "T-2" }, entries.Select(e => e.Item!.Id));
        Assert.All(entries, e => Assert.StartsWith("Priority: ", e.PriorityGroup));
        Assert.Contains("·", entries[0].DisplayLine);
    }

    /// <summary>Priority index 1 keeps only high-priority entries.</summary>
    [Fact]
    public void Project_FiltersByPriorityIndex()
    {
        var entries = Svc.BuildEntries(new[] { Item("T-1", "high", "A"), Item("T-2", "low", "B") });
        var groups = Svc.Project(entries, priorityIndex: 1, scopeIndex: 0, filterText: null);
        Assert.Equal(new[] { "T-1" }, groups.SelectMany(g => g.Items).Select(e => e.Item!.Id));
    }

    /// <summary>Title-scope text filter matches on title only.</summary>
    [Fact]
    public void Project_FiltersByText_TitleScope()
    {
        var entries = Svc.BuildEntries(new[] { Item("T-1", "high", "Alpha"), Item("T-2", "high", "Beta") });
        var groups = Svc.Project(entries, 0, 0, "Alpha");
        Assert.Equal(new[] { "T-1" }, groups.SelectMany(g => g.Items).Select(e => e.Item!.Id));
    }

    /// <summary>Id-scope text filter matches on id only.</summary>
    [Fact]
    public void Project_FiltersByText_IdScope()
    {
        var entries = Svc.BuildEntries(new[] { Item("ALPHA-1", "high", "x"), Item("BETA-2", "high", "y") });
        var groups = Svc.Project(entries, 0, 1, "BETA");
        Assert.Equal(new[] { "BETA-2" }, groups.SelectMany(g => g.Items).Select(e => e.Item!.Id));
    }

    /// <summary>Groups are ordered by priority (high group first).</summary>
    [Fact]
    public void Project_GroupsOrderedByPriority()
    {
        var entries = Svc.BuildEntries(new[] { Item("T-1", "low", "A"), Item("T-2", "high", "B") });
        var groups = Svc.Project(entries, 0, 0, null);
        Assert.Equal("Priority: High", groups.First().Name);
    }

    /// <summary>Empty filter returns all entries.</summary>
    [Fact]
    public void Project_NoFilter_ReturnsAll()
    {
        var entries = Svc.BuildEntries(new[] { Item("T-1", "high", "A"), Item("T-2", "low", "B") });
        var groups = Svc.Project(entries, 0, 0, "");
        Assert.Equal(2, groups.SelectMany(g => g.Items).Count());
    }
}
