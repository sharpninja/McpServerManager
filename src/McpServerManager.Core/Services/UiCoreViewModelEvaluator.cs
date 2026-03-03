using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServer.UI.Core.Messages;
using McpServerManager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using UiCoreTodoListViewModel = McpServer.UI.Core.ViewModels.TodoListViewModel;
using UiCoreWorkspaceContextViewModel = McpServer.UI.Core.ViewModels.WorkspaceContextViewModel;
using UiCoreWorkspaceListViewModel = McpServer.UI.Core.ViewModels.WorkspaceListViewModel;

namespace McpServerManager.Core.Services;

/// <summary>
/// Evaluates parity between RequestTracker list loading and McpServer.UI.Core list ViewModels.
/// </summary>
internal sealed class UiCoreViewModelEvaluator
{
    private readonly McpTodoService? _todoService;
    private readonly McpWorkspaceService? _workspaceService;

    public UiCoreViewModelEvaluator(
        McpTodoService? todoService = null,
        McpWorkspaceService? workspaceService = null)
    {
        _todoService = todoService;
        _workspaceService = workspaceService;
    }

    public async Task<UiCoreListEvaluationResult> EvaluateTodoListAsync(
        McpTodoQueryResult currentResult,
        bool includeCompleted,
        CancellationToken cancellationToken = default)
    {
        if (_todoService is null)
            return UiCoreListEvaluationResult.Failure("TODO service is not available.", "todo");
        if (currentResult == null)
            throw new ArgumentNullException(nameof(currentResult));

        try
        {
            var dispatcher = new Dispatcher(
                new TodoQueryServiceProvider(new UiCoreTodoListQueryHandler(_todoService)),
                NullLogger<Dispatcher>.Instance);

            var vm = new UiCoreTodoListViewModel(
                dispatcher,
                new UiCoreWorkspaceContextViewModel(),
                NullLogger<UiCoreTodoListViewModel>.Instance)
            {
                Done = includeCompleted ? null : false
            };

            await vm.LoadAsync(cancellationToken).ConfigureAwait(false);

            var currentIds = NormalizeIds(currentResult.Items.Select(static item => item.Id));
            var uiCoreIds = NormalizeIds(vm.Items.Select(static item => item.Id));
            return Compare("todo", currentIds, uiCoreIds);
        }
        catch (Exception ex)
        {
            return UiCoreListEvaluationResult.Failure(ex.Message, "todo");
        }
    }

    public async Task<UiCoreListEvaluationResult> EvaluateWorkspaceListAsync(
        McpWorkspaceQueryResult currentResult,
        CancellationToken cancellationToken = default)
    {
        if (_workspaceService is null)
            return UiCoreListEvaluationResult.Failure("Workspace service is not available.", "workspace");
        if (currentResult == null)
            throw new ArgumentNullException(nameof(currentResult));

        try
        {
            var dispatcher = new Dispatcher(
                new WorkspaceQueryServiceProvider(new UiCoreWorkspaceListQueryHandler(_workspaceService)),
                NullLogger<Dispatcher>.Instance);

            var vm = new UiCoreWorkspaceListViewModel(
                dispatcher,
                NullLogger<UiCoreWorkspaceListViewModel>.Instance);

            await vm.LoadAsync(cancellationToken).ConfigureAwait(false);

            var currentIds = NormalizeIds(currentResult.Items.Select(static item => item.WorkspacePath));
            var uiCoreIds = NormalizeIds(vm.Workspaces.Select(static item => item.WorkspacePath));
            return Compare("workspace", currentIds, uiCoreIds);
        }
        catch (Exception ex)
        {
            return UiCoreListEvaluationResult.Failure(ex.Message, "workspace");
        }
    }

    private static UiCoreListEvaluationResult Compare(
        string area,
        IReadOnlyList<string> currentIds,
        IReadOnlyList<string> uiCoreIds)
    {
        var missingInUiCore = currentIds
            .Except(uiCoreIds, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingInCurrent = uiCoreIds
            .Except(currentIds, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UiCoreListEvaluationResult
        {
            Area = area,
            Success = true,
            CurrentCount = currentIds.Count,
            UiCoreCount = uiCoreIds.Count,
            MissingInUiCore = missingInUiCore,
            MissingInCurrent = missingInCurrent,
            IsMatch = missingInUiCore.Length == 0 && missingInCurrent.Length == 0
        };
    }

    private static string[] NormalizeIds(IEnumerable<string?> ids)
    {
        return ids
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class UiCoreTodoListQueryHandler : IQueryHandler<ListTodosQuery, ListTodosResult>
    {
        private readonly McpTodoService _service;

        public UiCoreTodoListQueryHandler(McpTodoService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async Task<Result<ListTodosResult>> HandleAsync(ListTodosQuery query, CallContext context)
        {
            try
            {
                var result = await _service.QueryAsync(
                        keyword: query.Keyword,
                        priority: query.Priority,
                        section: query.Section,
                        id: query.Id,
                        done: query.Done,
                        cancellationToken: context.CancellationToken)
                    .ConfigureAwait(false);

                var items = result.Items
                    .Select(static item => new TodoListItem(
                        Id: item.Id,
                        Title: item.Title,
                        Section: item.Section,
                        Priority: item.Priority,
                        Done: item.Done,
                        Estimate: item.Estimate))
                    .ToList();

                return Result<ListTodosResult>.Success(new ListTodosResult(items, result.TotalCount));
            }
            catch (Exception ex)
            {
                return Result<ListTodosResult>.Failure(ex);
            }
        }
    }

    private sealed class UiCoreWorkspaceListQueryHandler : IQueryHandler<ListWorkspacesQuery, ListWorkspacesResult>
    {
        private readonly McpWorkspaceService _service;

        public UiCoreWorkspaceListQueryHandler(McpWorkspaceService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async Task<Result<ListWorkspacesResult>> HandleAsync(ListWorkspacesQuery query, CallContext context)
        {
            try
            {
                var result = await _service.QueryAsync(context.CancellationToken).ConfigureAwait(false);

                var items = result.Items
                    .Where(static item => !string.IsNullOrWhiteSpace(item.WorkspacePath))
                    .Select(static item => new WorkspaceSummary(
                        WorkspacePath: item.WorkspacePath!,
                        Name: item.Name ?? string.Empty,
                        IsPrimary: item.IsPrimary ?? false,
                        IsEnabled: item.IsEnabled ?? true))
                    .ToList();

                return Result<ListWorkspacesResult>.Success(new ListWorkspacesResult(items, items.Count));
            }
            catch (Exception ex)
            {
                return Result<ListWorkspacesResult>.Failure(ex);
            }
        }
    }

    private sealed class TodoQueryServiceProvider : IServiceProvider
    {
        private readonly IQueryHandler<ListTodosQuery, ListTodosResult> _todoHandler;

        public TodoQueryServiceProvider(IQueryHandler<ListTodosQuery, ListTodosResult> todoHandler)
        {
            _todoHandler = todoHandler ?? throw new ArgumentNullException(nameof(todoHandler));
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IQueryHandler<ListTodosQuery, ListTodosResult>))
                return _todoHandler;
            if (serviceType == typeof(IEnumerable<IPipelineBehavior>))
                return Array.Empty<IPipelineBehavior>();
            return null;
        }
    }

    private sealed class WorkspaceQueryServiceProvider : IServiceProvider
    {
        private readonly IQueryHandler<ListWorkspacesQuery, ListWorkspacesResult> _workspaceHandler;

        public WorkspaceQueryServiceProvider(IQueryHandler<ListWorkspacesQuery, ListWorkspacesResult> workspaceHandler)
        {
            _workspaceHandler = workspaceHandler ?? throw new ArgumentNullException(nameof(workspaceHandler));
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IQueryHandler<ListWorkspacesQuery, ListWorkspacesResult>))
                return _workspaceHandler;
            if (serviceType == typeof(IEnumerable<IPipelineBehavior>))
                return Array.Empty<IPipelineBehavior>();
            return null;
        }
    }
}

internal sealed class UiCoreListEvaluationResult
{
    public string Area { get; init; } = "";
    public bool Success { get; init; }
    public bool IsMatch { get; init; }
    public int CurrentCount { get; init; }
    public int UiCoreCount { get; init; }
    public IReadOnlyList<string> MissingInUiCore { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingInCurrent { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }

    public static UiCoreListEvaluationResult Failure(string error, string area) =>
        new()
        {
            Area = area,
            Success = false,
            IsMatch = false,
            CurrentCount = 0,
            UiCoreCount = 0,
            Error = error
        };
}
