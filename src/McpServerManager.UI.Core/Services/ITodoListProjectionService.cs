using System.Collections.Generic;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// TR-HANDLER-EXTRACTION / FR-VM-HANDLER-MUTATION (PLAN-C2-TODO-FAMILY-001): pure projection of
/// TODO list items into display entries and filtered/grouped view state. Extracted from
/// <c>TodoListHostViewModel</c> so the ViewModel holds state and delegation only (TR-VM-STATE-ONLY).
/// </summary>
public interface ITodoListProjectionService
{
    /// <summary>Maps raw list items into display entries sorted by priority then id.</summary>
    /// <param name="items">The raw TODO list items.</param>
    /// <returns>Display entries in priority/id order.</returns>
    IReadOnlyList<TodoListEntry> BuildEntries(IEnumerable<TodoListItem> items);

    /// <summary>Filters and groups entries by priority index, scope index, and free-text filter.</summary>
    /// <param name="entries">The full entry set to project.</param>
    /// <param name="priorityIndex">0=all, 1=high, 2=medium, 3=low.</param>
    /// <param name="scopeIndex">0=title, 1=id, 2=all fields.</param>
    /// <param name="filterText">Optional boolean search text.</param>
    /// <returns>Priority-ordered groups of matching entries.</returns>
    IReadOnlyList<TodoListGroup> Project(
        IReadOnlyList<TodoListEntry> entries,
        int priorityIndex,
        int scopeIndex,
        string? filterText);
}
