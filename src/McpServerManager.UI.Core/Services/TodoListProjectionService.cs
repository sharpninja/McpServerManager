using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Pure implementation of <see cref="ITodoListProjectionService"/>. Contains the entry-building,
/// filtering, and grouping logic extracted from <c>TodoListHostViewModel</c>
/// (PLAN-C2-TODO-FAMILY-001, TR-HANDLER-EXTRACTION). No dependencies, no side effects.
/// </summary>
public sealed class TodoListProjectionService : ITodoListProjectionService
{
    /// <inheritdoc />
    public IReadOnlyList<TodoListEntry> BuildEntries(IEnumerable<TodoListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select(static item =>
            {
                var flat = new McpTodoFlatItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    Section = item.Section,
                    Priority = item.Priority,
                    Done = item.Done,
                    Estimate = item.Estimate
                };

                return new TodoListEntry
                {
                    PriorityGroup = "Priority: " + FormatPriority(flat.Priority),
                    DisplayLine = $"{flat.Id} · {flat.Priority} · {flat.Title}",
                    Item = flat
                };
            })
            .OrderBy(e => PrioritySortKey(e.Item?.Priority))
            .ThenBy(e => e.Item?.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TodoListGroup> Project(
        IReadOnlyList<TodoListEntry> entries,
        int priorityIndex,
        int scopeIndex,
        string? filterText)
    {
        ArgumentNullException.ThrowIfNull(entries);

        IEnumerable<TodoListEntry> source = entries;

        var priorityTag = priorityIndex switch
        {
            1 => "high",
            2 => "medium",
            3 => "low",
            _ => ""
        };

        if (!string.IsNullOrEmpty(priorityTag))
        {
            source = source.Where(e =>
                string.Equals(e.Item?.Priority, priorityTag, StringComparison.OrdinalIgnoreCase));
        }

        var text = (filterText ?? "").Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var scopeTag = scopeIndex switch
            {
                1 => "id",
                2 => "all",
                _ => "title"
            };
            var matcher = BooleanSearchParser.Parse(text);
            source = source.Where(e => MatchesTextFilter(e.Item, matcher, scopeTag));
        }

        return source
            .ToList()
            .GroupBy(e => e.PriorityGroup)
            .OrderBy(g => PrioritySortKey(g.First().Item?.Priority))
            .Select(g => new TodoListGroup(
                g.Key,
                new ObservableCollection<TodoListEntry>(
                    g.OrderBy(e => e.Item?.Id, StringComparer.OrdinalIgnoreCase))))
            .ToList();
    }

    private static bool MatchesTextFilter(McpTodoFlatItem? item, Func<string, bool> matcher, string scope)
    {
        if (item == null)
            return false;

        var searchable = scope switch
        {
            "id" => item.Id ?? "",
            "title" => item.Title ?? "",
            _ => string.Join(
                " ",
                new[] { item.Id, item.Title, item.Section, item.Priority, item.Note, item.Estimate, item.Remaining }
                    .Concat(item.Description ?? Enumerable.Empty<string>())
                    .Concat(item.TechnicalDetails ?? Enumerable.Empty<string>())
                    .Where(s => !string.IsNullOrEmpty(s)))
        };

        return matcher(searchable);
    }

    private static string FormatPriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return "Other";

        return char.ToUpperInvariant(priority[0]) + priority.Substring(1).ToLowerInvariant();
    }

    private static int PrioritySortKey(string? priority) => (priority?.Trim().ToUpperInvariant()) switch
    {
        "HIGH" => 0,
        "MEDIUM" => 1,
        "LOW" => 2,
        _ => 3
    };
}
