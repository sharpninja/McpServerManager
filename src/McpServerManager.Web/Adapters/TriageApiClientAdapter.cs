using McpServer.Client;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Web.Adapters;

internal sealed class TriageApiClientAdapter : ITriageApiClient
{
    private readonly WebMcpContext _context;
    private readonly ILogger<TriageApiClientAdapter> _logger;

    public TriageApiClientAdapter(WebMcpContext context, ILogger<TriageApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<TriageApiClientAdapter>.Instance;
    }

    public async Task<TriageDashboardSnapshot> GetDashboardAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var resolvedWorkspacePath = ResolveWorkspacePath(workspacePath);
        var result = await _context.UseWorkspaceApiClientAsync(
                resolvedWorkspacePath,
                (client, ct) => client.Triage.GetDashboardAsync(resolvedWorkspacePath, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new TriageDashboardSnapshot(
            result.TriageQueue.Select(MapGroup).ToList(),
            result.ReportGroupQueue.Select(MapGroup).ToList(),
            result.RunHistory.Select(MapRun).ToList(),
            result.TotalGroupCount,
            result.TotalRunCount);
    }

    public async Task<TriageGroupQuerySnapshot> QueryGroupsAsync(string? status, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var resolvedWorkspacePath = ResolveWorkspacePath(workspacePath);
        var result = await _context.UseWorkspaceApiClientAsync(
                resolvedWorkspacePath,
                (client, ct) => client.Triage.QueryGroupsAsync(Normalize(status), resolvedWorkspacePath, ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new TriageGroupQuerySnapshot(result.Items.Select(MapGroup).ToList(), result.TotalCount);
    }

    public async Task<TriageGroupSnapshot?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseWorkspaceApiClientAsync(
                    ResolveWorkspacePath(null),
                    (client, ct) => client.Triage.GetGroupAsync(groupId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MapGroup(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<TriageReportSnapshot?> GetReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseWorkspaceApiClientAsync(
                    ResolveWorkspacePath(null),
                    (client, ct) => client.Triage.GetReportAsync(reportId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MapReport(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<TriageRunQuerySnapshot> QueryRunsAsync(string? status, string? groupId, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var resolvedWorkspacePath = ResolveWorkspacePath(workspacePath);
        var result = await _context.UseWorkspaceApiClientAsync(
                resolvedWorkspacePath,
                (client, ct) => client.Triage.QueryRunsAsync(Normalize(status), Normalize(groupId), resolvedWorkspacePath, ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new TriageRunQuerySnapshot(result.Items.Select(MapRun).ToList(), result.TotalCount);
    }

    public async Task<TriageRunSnapshot?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseWorkspaceApiClientAsync(
                    ResolveWorkspacePath(null),
                    (client, ct) => client.Triage.GetRunAsync(runId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MapRun(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<OpenTriageTodosResult> QueryOpenCreatedTodosAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var resolvedWorkspacePath = ResolveWorkspacePath(workspacePath);
        var created = await _context.UseWorkspaceApiClientAsync(
                resolvedWorkspacePath,
                (client, ct) => client.Triage.QueryCreatedTodosAsync(resolvedWorkspacePath, ct),
                cancellationToken)
            .ConfigureAwait(true);

        var items = new List<OpenTriageTodoItem>();
        var hidden = 0;
        var hydrationErrors = 0;

        foreach (var createdTodo in created.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var todoId = Normalize(createdTodo.TodoId);
            var todoWorkspace = Normalize(createdTodo.WorkspacePath) ?? resolvedWorkspacePath;
            if (string.IsNullOrWhiteSpace(todoId))
            {
                hydrationErrors++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(todoWorkspace))
            {
                hydrationErrors++;
                items.Add(MapCreatedTodoReference(createdTodo, string.Empty, canOpen: false));
                continue;
            }

            try
            {
                var todo = await _context.UseWorkspaceApiClientAsync(
                        todoWorkspace,
                        (client, ct) => client.Todo.GetAsync(todoId, ct),
                        cancellationToken)
                    .ConfigureAwait(true);
                if (todo.Done)
                {
                    hidden++;
                }

                items.Add(MapOpenTodo(createdTodo, todo, todoWorkspace));
            }
            catch (McpNotFoundException ex)
            {
                _logger.LogInformation("{ExceptionDetail}", ex.ToString());
                hidden++;
                items.Add(MapCreatedTodoReference(createdTodo, todoWorkspace, canOpen: false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("{ExceptionDetail}", ex.ToString());
                hydrationErrors++;
                items.Add(MapCreatedTodoReference(createdTodo, todoWorkspace, canOpen: false));
            }
        }

        return new OpenTriageTodosResult(items, created.TotalCount, hidden, hydrationErrors);
    }

    public async Task<TriageGroupEditResultSnapshot> CreateGroupFromSelectionAsync(TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseWorkspaceApiClientAsync(
                ResolveWorkspacePath(null),
                (client, ct) => client.Triage.CreateGroupFromSelectionAsync(MapSelection(selection), ct),
                cancellationToken)
            .ConfigureAwait(true);
        return MapEditResult(result);
    }

    public async Task<TriageGroupEditResultSnapshot> ConsolidateIntoGroupAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseWorkspaceApiClientAsync(
                ResolveWorkspacePath(null),
                (client, ct) => client.Triage.ConsolidateIntoGroupAsync(targetGroupId, MapSelection(selection), ct),
                cancellationToken)
            .ConfigureAwait(true);
        return MapEditResult(result);
    }

    public async Task<TriageGroupEditResultSnapshot> MergeGroupsAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseWorkspaceApiClientAsync(
                ResolveWorkspacePath(null),
                (client, ct) => client.Triage.MergeGroupsAsync(targetGroupId, MapSelection(selection), ct),
                cancellationToken)
            .ConfigureAwait(true);
        return MapEditResult(result);
    }

    public async Task<TriageGroupSnapshot> RetryGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseWorkspaceApiClientAsync(
                ResolveWorkspacePath(null),
                (client, ct) => client.Triage.RetryGroupAsync(groupId, ct),
                cancellationToken)
            .ConfigureAwait(true);
        return MapGroup(result);
    }

    private static TriageGroupSnapshot MapGroup(TriageGroupDetail item)
        => new(
            item.GroupId,
            item.Status,
            item.ReportCount,
            item.WorkspacePath,
            item.Title,
            item.Summary,
            item.QuietDeadlineUtc,
            item.CreatedTodoId,
            item.LastError,
            item.Reports.Select(MapReport).ToList());

    private static TriageReportSnapshot MapReport(TriageReportDetail item)
        => new(
            item.ReportId,
            item.GroupId,
            item.Status,
            item.Title,
            item.Summary,
            item.OriginalWorkspacePath,
            item.WorkspacePath,
            item.CreatedUtc);

    private static TriageRunSnapshot MapRun(TriageResearchRunDetail item)
        => new(
            item.RunId,
            item.GroupId,
            item.Status,
            item.WorkspacePath,
            item.GroupStatus,
            item.GroupTitle,
            item.GroupSummary,
            item.ReportCount,
            item.PromptTemplateId,
            item.Prompt,
            item.GroupJson,
            item.RawOutput,
            item.ResponseJson,
            item.Error,
            item.CreatedTodoId,
            item.StartedUtc,
            item.CompletedUtc);

    private static OpenTriageTodoItem MapOpenTodo(TriageCreatedTodoDetail createdTodo, TodoFlatItem todo, string workspacePath)
        => new(
            createdTodo.TodoId,
            todo.Title,
            workspacePath,
            todo.Section,
            todo.Priority,
            createdTodo.GroupId,
            createdTodo.RunId,
            createdTodo.GroupStatus,
            createdTodo.RunStatus,
            createdTodo.CreatedAtUtc,
            createdTodo.GroupTitle,
            createdTodo.GroupSummary,
            createdTodo.ReportCount,
            createdTodo.QuietDeadlineUtc,
            todo.Done,
            CanOpen: true);

    private static OpenTriageTodoItem MapCreatedTodoReference(TriageCreatedTodoDetail createdTodo, string workspacePath, bool canOpen)
        => new(
            createdTodo.TodoId,
            createdTodo.GroupTitle ?? createdTodo.GroupSummary ?? createdTodo.TodoId,
            workspacePath,
            Section: null,
            Priority: null,
            createdTodo.GroupId,
            createdTodo.RunId,
            createdTodo.GroupStatus,
            createdTodo.RunStatus,
            createdTodo.CreatedAtUtc,
            createdTodo.GroupTitle,
            createdTodo.GroupSummary,
            createdTodo.ReportCount,
            createdTodo.QuietDeadlineUtc,
            Done: false,
            CanOpen: canOpen);

    private static TriageGroupEditResultSnapshot MapEditResult(TriageGroupEditResult result)
        => new(MapGroup(result.Group), result.RemovedGroupIds, result.MovedReportCount);

    private static TriageGroupSelectionRequest MapSelection(TriageGroupSelectionSnapshot selection)
        => new()
        {
            GroupIds = selection.GroupIds.Count == 0 ? null : selection.GroupIds,
            ReportIds = selection.ReportIds.Count == 0 ? null : selection.ReportIds,
            Title = Normalize(selection.Title),
            Summary = Normalize(selection.Summary),
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string? ResolveWorkspacePath(string? workspacePath)
        => Normalize(workspacePath) ?? Normalize(_context.ActiveWorkspacePath);
}
