using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using McpServer.VsExtension.McpTodo.Models;

namespace McpServer.VsExtension.McpTodo;

internal sealed class VsixTodoApiClientAdapter(McpTodoClient client) : ITodoApiClient
{
    private readonly McpTodoClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<ListTodosResult> ListTodosAsync(ListTodosQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!string.IsNullOrWhiteSpace(query.Id))
        {
            var item = await _client.GetTodoByIdAsync(query.Id.Trim(), cancellationToken).ConfigureAwait(true);
            var filteredById = item is null
                ? []
                : ApplyLocalFilters([item], query).Select(MapListItem).ToList();
            return new ListTodosResult(filteredById, filteredById.Count);
        }

        var result = await _client.GetTodoListAsync(query.Done, query.Priority, query.Keyword, cancellationToken).ConfigureAwait(true);
        var filtered = ApplyLocalFilters(result.Items ?? [], query)
            .Select(MapListItem)
            .ToList();

        return new ListTodosResult(filtered, filtered.Count);
    }

    public async Task<TodoDetail?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default)
    {
        var item = await _client.GetTodoByIdAsync(todoId, cancellationToken).ConfigureAwait(true);
        return item is null ? null : MapDetail(item);
    }

    public async Task<TodoMutationOutcome> UpdateTodoAsync(UpdateTodoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await _client.UpdateTodoAsync(command.TodoId, Map(command), cancellationToken).ConfigureAwait(true);
        return new TodoMutationOutcome(result.Success, result.Error, result.Item is null ? null : MapDetail(result.Item));
    }

    public Task<TodoMutationOutcome> CreateTodoAsync(CreateTodoCommand command, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("TODO creation is handled by the Visual Studio editor workflow in the VSIX host.");

    public Task<TodoMutationOutcome> DeleteTodoAsync(DeleteTodoCommand command, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("TODO deletion is not currently exposed by the VSIX host.");

    public Task<TodoRequirementsAnalysis> AnalyzeTodoRequirementsAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("TODO requirements analysis is not currently exposed by the VSIX host.");

    public Task<TodoPromptOutput> GenerateTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    public Task<TodoPromptOutput> GenerateTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    public Task<TodoPromptOutput> GenerateTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    public IAsyncEnumerable<string> StreamTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    public IAsyncEnumerable<string> StreamTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    public IAsyncEnumerable<string> StreamTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot prompt generation is handled by the VSIX host wrapper.");

    private static IEnumerable<TodoFlatItem> ApplyLocalFilters(IEnumerable<TodoFlatItem> items, ListTodosQuery query)
    {
        var filtered = items;

        if (!string.IsNullOrWhiteSpace(query.Section))
        {
            filtered = filtered.Where(item =>
                string.Equals(item.Section, query.Section, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Done.HasValue)
        {
            filtered = filtered.Where(item => item.Done == query.Done.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            filtered = filtered.Where(item =>
                string.Equals(item.Priority, query.Priority, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            filtered = filtered.Where(item => MatchesKeyword(item, keyword));
        }

        return filtered;
    }

    private static bool MatchesKeyword(TodoFlatItem item, string keyword)
    {
        var values = new string?[]
            {
                item.Id,
                item.Title,
                item.Section,
                item.Priority,
                item.Note,
                item.Estimate,
                item.Remaining,
                item.Reference,
                item.CompletedDate,
                item.DoneSummary
            }
            .Concat(item.Description ?? Array.Empty<string>())
            .Concat(item.TechnicalDetails ?? Array.Empty<string>())
            .Concat(item.DependsOn ?? Array.Empty<string>())
            .Concat(item.FunctionalRequirements ?? Array.Empty<string>())
            .Concat(item.TechnicalRequirements ?? Array.Empty<string>())
            .OfType<string>();

        return values.Any(value => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static TodoListItem MapListItem(TodoFlatItem item)
        => new(item.Id, item.Title, item.Section, item.Priority, item.Done, item.Estimate);

    private static TodoDetail MapDetail(TodoFlatItem item)
        => new(
            item.Id,
            item.Title,
            item.Section,
            item.Priority,
            item.Done,
            item.Estimate,
            item.Note,
            item.Description?.ToList() ?? [],
            item.TechnicalDetails?.ToList() ?? [],
            item.ImplementationTasks?.Select(static task => new TodoTaskDetail(task.Task, task.Done)).ToList() ?? [],
            item.CompletedDate,
            item.DoneSummary,
            item.Remaining,
            null,
            item.Reference,
            item.DependsOn?.ToList() ?? [],
            item.FunctionalRequirements?.ToList() ?? [],
            item.TechnicalRequirements?.ToList() ?? []);

    private static TodoUpdateBody Map(UpdateTodoCommand command)
        => new()
        {
            Title = command.Title,
            Priority = command.Priority,
            Section = command.Section,
            Done = command.Done,
            Estimate = command.Estimate,
            Description = command.Description?.ToList(),
            TechnicalDetails = command.TechnicalDetails?.ToList(),
            ImplementationTasks = command.ImplementationTasks?.Select(static task => new TodoFlatTask
            {
                Task = task.Task,
                Done = task.Done
            }).ToList(),
            Note = command.Note,
            CompletedDate = command.CompletedDate,
            DoneSummary = command.DoneSummary,
            Remaining = command.Remaining,
            DependsOn = command.DependsOn?.ToList(),
            FunctionalRequirements = command.FunctionalRequirements?.ToList(),
            TechnicalRequirements = command.TechnicalRequirements?.ToList(),
        };
}
