using McpServer.Client;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Director;

/// <summary>Director adapter for read-only triage dashboard operations.</summary>
internal sealed class TriageApiClientAdapter : ITriageApiClient
{
    private readonly DirectorMcpContext _context;
    private readonly ILogger<TriageApiClientAdapter> _logger;

    public TriageApiClientAdapter(DirectorMcpContext context, ILogger<TriageApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<TriageApiClientAdapter>.Instance;
    }

    public async Task<TriageDashboardSnapshot> GetDashboardAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var result = await UseTriageClientAsync(
            (client, ct) => client.Triage.GetDashboardAsync(Normalize(workspacePath), ct),
            cancellationToken).ConfigureAwait(true);

        return new TriageDashboardSnapshot(
            result.TriageQueue.Select(MapGroup).ToList(),
            result.ReportGroupQueue.Select(MapGroup).ToList(),
            result.RunHistory.Select(MapRun).ToList(),
            result.TotalGroupCount,
            result.TotalRunCount);
    }

    public async Task<TriageGroupQuerySnapshot> QueryGroupsAsync(string? status, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var result = await UseTriageClientAsync(
            (client, ct) => client.Triage.QueryGroupsAsync(Normalize(status), Normalize(workspacePath), ct),
            cancellationToken).ConfigureAwait(true);
        return new TriageGroupQuerySnapshot(result.Items.Select(MapGroup).ToList(), result.TotalCount);
    }

    public async Task<TriageGroupSnapshot?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await UseTriageClientAsync(
                (client, ct) => client.Triage.GetGroupAsync(groupId, ct),
                cancellationToken).ConfigureAwait(true);
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
            var result = await UseTriageClientAsync(
                (client, ct) => client.Triage.GetReportAsync(reportId, ct),
                cancellationToken).ConfigureAwait(true);
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
        var result = await UseTriageClientAsync(
            (client, ct) => client.Triage.QueryRunsAsync(Normalize(status), Normalize(groupId), Normalize(workspacePath), ct),
            cancellationToken).ConfigureAwait(true);
        return new TriageRunQuerySnapshot(result.Items.Select(MapRun).ToList(), result.TotalCount);
    }

    public async Task<TriageRunSnapshot?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await UseTriageClientAsync(
                (client, ct) => client.Triage.GetRunAsync(runId, ct),
                cancellationToken).ConfigureAwait(true);
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
        var created = await UseTriageClientAsync(
            (client, ct) => client.Triage.QueryCreatedTodosAsync(Normalize(workspacePath), ct),
            cancellationToken).ConfigureAwait(true);

        var items = new List<OpenTriageTodoItem>();
        var hidden = 0;
        var hydrationErrors = 0;

        foreach (var createdTodo in created.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var todoId = Normalize(createdTodo.TodoId);
            var todoWorkspace = Normalize(createdTodo.WorkspacePath) ?? Normalize(workspacePath);
            if (string.IsNullOrWhiteSpace(todoId) || string.IsNullOrWhiteSpace(todoWorkspace))
            {
                hydrationErrors++;
                continue;
            }

            try
            {
                var todo = await _context.UseWorkspaceApiClientAsync(
                    todoWorkspace,
                    (client, ct) => client.Todo.GetAsync(todoId, ct),
                    cancellationToken).ConfigureAwait(true);
                if (todo.Done)
                {
                    hidden++;
                    continue;
                }

                items.Add(MapOpenTodo(createdTodo, todo, todoWorkspace));
            }
            catch (McpNotFoundException ex)
            {
                _logger.LogInformation("{ExceptionDetail}", ex.ToString());
                hidden++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("{ExceptionDetail}", ex.ToString());
                hydrationErrors++;
            }
        }

        return new OpenTriageTodosResult(items, created.TotalCount, hidden, hydrationErrors);
    }

    private async Task<T> UseTriageClientAsync<T>(
        Func<McpServerClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var client = _context.HasControlConnection
            ? await _context.GetRequiredControlApiClientAsync(cancellationToken).ConfigureAwait(true)
            : await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
        return await operation(client, cancellationToken).ConfigureAwait(true);
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
            createdTodo.QuietDeadlineUtc);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
