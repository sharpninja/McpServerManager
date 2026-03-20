using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServer.Cqrs.Mvvm;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Models;
using McpServer.UI.Core.Models.Json;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Web;

/// <summary>
/// Web-host command target that routes page actions through <see cref="CqrsRelayCommand{TResult}"/>
/// while surfacing unsupported desktop-shell operations explicitly.
/// </summary>
internal sealed class WebCommandTarget : ICommandTarget
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WebCommandTarget> _logger;
    private readonly IUiDispatcherService _uiDispatcher;

    public WebCommandTarget(Dispatcher dispatcher, ILogger<WebCommandTarget> logger, IUiDispatcherService uiDispatcher)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _uiDispatcher = uiDispatcher;
    }

    public string StatusMessage { get; set; } = string.Empty;

    public McpSessionLogService McpSessionService
        => throw CreateUnsupportedException(nameof(McpSessionService));

    public JsonLogSummary JsonLogSummary { get; set; } = new();

    public ObservableCollection<JsonTreeNode> JsonTree { get; } = new();

    public string AgentFilter { get; set; } = string.Empty;

    public CqrsRelayCommand<bool> Create(Action action, Func<bool>? canExecute = null)
        => CqrsRelayFactory.Create(_dispatcher, action, canExecute);

    public CqrsRelayCommand<bool> Create(Func<Task> action, Func<bool>? canExecute = null)
        => CqrsRelayFactory.Create(_dispatcher, action, canExecute);

    public CqrsRelayCommand<bool> Create<T>(Action<T?> action, Func<T?, bool>? canExecute = null)
        => CqrsRelayFactory.Create(_dispatcher, action, canExecute);

    public CqrsRelayCommand<bool> Create<T>(Func<T?, Task> action, Func<T?, bool>? canExecute = null)
        => CqrsRelayFactory.Create(_dispatcher, action, canExecute);

    public Task RunAsync(Action action, Func<bool>? canExecute = null, CancellationToken cancellationToken = default)
        => DispatchAsync(Create(action, canExecute), parameter: null, cancellationToken);

    public Task RunAsync(Func<Task> action, Func<bool>? canExecute = null, CancellationToken cancellationToken = default)
        => DispatchAsync(Create(action, canExecute), parameter: null, cancellationToken);

    public Task RunAsync<T>(Action<T?> action, T? parameter, Func<T?, bool>? canExecute = null, CancellationToken cancellationToken = default)
        => DispatchAsync(Create(action, canExecute), parameter, cancellationToken);

    public Task RunAsync<T>(Func<T?, Task> action, T? parameter, Func<T?, bool>? canExecute = null, CancellationToken cancellationToken = default)
        => DispatchAsync(Create(action, canExecute), parameter, cancellationToken);

    public void NavigateBack() => FireAndForget(DispatchUnsupportedAsync(nameof(NavigateBack)));

    public void NavigateForward() => FireAndForget(DispatchUnsupportedAsync(nameof(NavigateForward)));

    public Task RefreshAsync() => UnsupportedAsync(nameof(RefreshAsync));

    public void PhoneNavigateSection(string? sectionKey) => FireAndForget(DispatchUnsupportedAsync<string>(nameof(PhoneNavigateSection), sectionKey));

    public void GenerateAndNavigate(FileNode? node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(GenerateAndNavigate), node));

    public void OpenTreeItem(FileNode? node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(OpenTreeItem), node));

    public void TreeItemTapped(FileNode? node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(TreeItemTapped), node));

    public void JsonNodeDoubleTapped(JsonTreeNode? node) => FireAndForget(DispatchUnsupportedAsync<JsonTreeNode>(nameof(JsonNodeDoubleTapped), node));

    public void ShowRequestDetails(SearchableTurn entry) => FireAndForget(DispatchUnsupportedAsync<SearchableTurn>(nameof(ShowRequestDetails), entry));

    public void CloseRequestDetails() => FireAndForget(DispatchUnsupportedAsync(nameof(CloseRequestDetails)));

    public void NavigateToPreviousRequest() => FireAndForget(DispatchUnsupportedAsync(nameof(NavigateToPreviousRequest)));

    public void NavigateToNextRequest() => FireAndForget(DispatchUnsupportedAsync(nameof(NavigateToNextRequest)));

    public void SelectSearchTurn(SearchableTurn entry) => FireAndForget(DispatchUnsupportedAsync<SearchableTurn>(nameof(SelectSearchTurn), entry));

    public void SearchRowTapped(SearchableTurn? entry) => FireAndForget(DispatchUnsupportedAsync<SearchableTurn>(nameof(SearchRowTapped), entry));

    public void SearchRowDoubleTapped(SearchableTurn? entry) => FireAndForget(DispatchUnsupportedAsync<SearchableTurn>(nameof(SearchRowDoubleTapped), entry));

    public void OpenPreviewInBrowser() => FireAndForget(DispatchUnsupportedAsync(nameof(OpenPreviewInBrowser)));

    public void ToggleShowRawMarkdown() => FireAndForget(DispatchUnsupportedAsync(nameof(ToggleShowRawMarkdown)));

    public void Archive() => FireAndForget(DispatchUnsupportedAsync(nameof(Archive)));

    public void ArchiveTreeItem(FileNode? node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(ArchiveTreeItem), node));

    public Task ReloadFromMcpAsync() => UnsupportedAsync(nameof(ReloadFromMcpAsync));

    public void BuildUnifiedSummaryAndIndex(UnifiedSessionLog session, JsonLogSummary summary)
        => throw CreateUnsupportedException(nameof(BuildUnifiedSummaryAndIndex));

    public void BuildJsonTree(JsonNode? node, JsonTreeNode root, string? pathPrefix)
        => throw CreateUnsupportedException(nameof(BuildJsonTree));

    public void LoadJson(string filePath) => FireAndForget(DispatchUnsupportedAsync<string>(nameof(LoadJson), filePath));

    public void LoadMarkdownFile(FileNode node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(LoadMarkdownFile), node));

    public void LoadSourceFile(FileNode node) => FireAndForget(DispatchUnsupportedAsync<FileNode>(nameof(LoadSourceFile), node));

    public void UpdateFilteredSearchTurns()
        => throw CreateUnsupportedException(nameof(UpdateFilteredSearchTurns));

    public Dictionary<string, UnifiedSessionLog> BuildSessionsByPathDict(IReadOnlyList<UnifiedSessionLog> sessions)
        => throw CreateUnsupportedException(nameof(BuildSessionsByPathDict));

    public List<UnifiedSessionLog> OrderAndDeduplicateSessions(Dictionary<string, UnifiedSessionLog> byPath)
        => throw CreateUnsupportedException(nameof(OrderAndDeduplicateSessions));

    public void SetMcpSessionState(List<UnifiedSessionLog> sessions, Dictionary<string, UnifiedSessionLog> byPath)
        => throw CreateUnsupportedException(nameof(SetMcpSessionState));

    public Task CopyText(string text) => UnsupportedAsync(nameof(CopyText));

    public Task CopyOriginalJson(UnifiedSessionTurn? entry) => UnsupportedAsync(nameof(CopyOriginalJson));

    public void OpenAgentConfig() => FireAndForget(DispatchUnsupportedAsync(nameof(OpenAgentConfig)));

    public void OpenPromptTemplates() => FireAndForget(DispatchUnsupportedAsync(nameof(OpenPromptTemplates)));

    public void DispatchToUi(Action action) => _uiDispatcher.Post(action);

    public void TrackBackgroundWork(Task task)
        => FireAndForget(ObserveBackgroundWorkAsync(task));

    public Task CopilotStatusAsync() => UnsupportedAsync(nameof(CopilotStatusAsync));

    public Task CopilotPlanAsync() => UnsupportedAsync(nameof(CopilotPlanAsync));

    public Task CopilotImplementAsync() => UnsupportedAsync(nameof(CopilotImplementAsync));

    private Task DispatchUnsupportedAsync(string operationName)
        => DispatchAsync(Create(() => throw CreateUnsupportedException(operationName)), parameter: null, CancellationToken.None);

    private Task DispatchUnsupportedAsync<T>(string operationName, T? parameter)
        => DispatchAsync(Create<T>(_ => throw CreateUnsupportedException(operationName)), parameter, CancellationToken.None);

    private Task UnsupportedAsync(string operationName)
    {
        var exception = CreateUnsupportedException(operationName);
        StatusMessage = exception.Message;
        _logger.LogError(exception, "Web command target operation failed.");
        return Task.FromException(exception);
    }

    private async Task DispatchAsync(CqrsRelayCommand<bool> command, object? parameter, CancellationToken cancellationToken)
    {
        try
        {
            var result = await command.DispatchAsync(parameter, cancellationToken).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                var message = result.Error ?? "Web command execution failed.";
                StatusMessage = message;
                throw new InvalidOperationException(message);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.LogError(ex, "Web command target operation failed.");
            throw;
        }
    }

    private async Task ObserveBackgroundWorkAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.LogError(ex, "Tracked background work failed in the Web host.");
            throw;
        }
    }

    private void FireAndForget(Task task)
        => _ = SwallowAfterLoggingAsync(task);

    private static async Task SwallowAfterLoggingAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static NotSupportedException CreateUnsupportedException(string operationName)
        => new($"{operationName} is not supported in the Web host.");
}
