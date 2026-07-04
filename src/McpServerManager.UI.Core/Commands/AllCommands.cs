using System;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Commands;

// --- Navigation Commands ---

public sealed record NavigateBackCommand() : ICommand<bool>;

public sealed class NavigateBackHandler(INavigationTarget target) : ICommandHandler<NavigateBackCommand, bool>
{
    public Task<Result<bool>> HandleAsync(NavigateBackCommand command, CallContext context)
    {
        target.NavigateBack();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record NavigateForwardCommand() : ICommand<bool>;

public sealed class NavigateForwardHandler(INavigationTarget target) : ICommandHandler<NavigateForwardCommand, bool>
{
    public Task<Result<bool>> HandleAsync(NavigateForwardCommand command, CallContext context)
    {
        target.NavigateForward();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Refresh ---

public sealed record RefreshViewCommand() : ICommand<bool>;

public sealed class RefreshViewHandler(INavigationTarget target) : ICommandHandler<RefreshViewCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RefreshViewCommand command, CallContext context)
    {
        await target.RefreshAsync();
        return Result<bool>.Success(true);
    }
}

// --- Request Details ---

public sealed record ShowRequestDetailsCommand(Models.Json.SearchableTurn Entry) : ICommand<bool>;

public sealed class ShowRequestDetailsHandler(IRequestDetailsTarget target) : ICommandHandler<ShowRequestDetailsCommand, bool>
{
    public Task<Result<bool>> HandleAsync(ShowRequestDetailsCommand command, CallContext context)
    {
        target.ShowRequestDetails(command.Entry);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record CloseRequestDetailsCommand() : ICommand<bool>;

public sealed class CloseRequestDetailsHandler(IRequestDetailsTarget target) : ICommandHandler<CloseRequestDetailsCommand, bool>
{
    public Task<Result<bool>> HandleAsync(CloseRequestDetailsCommand command, CallContext context)
    {
        target.CloseRequestDetails();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record NavigateToPreviousRequestCommand() : ICommand<bool>;

public sealed class NavigateToPreviousRequestHandler(IRequestDetailsTarget target) : ICommandHandler<NavigateToPreviousRequestCommand, bool>
{
    public Task<Result<bool>> HandleAsync(NavigateToPreviousRequestCommand command, CallContext context)
    {
        target.NavigateToPreviousRequest();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record NavigateToNextRequestCommand() : ICommand<bool>;

public sealed class NavigateToNextRequestHandler(IRequestDetailsTarget target) : ICommandHandler<NavigateToNextRequestCommand, bool>
{
    public Task<Result<bool>> HandleAsync(NavigateToNextRequestCommand command, CallContext context)
    {
        target.NavigateToNextRequest();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Selection ---

public sealed record SelectSearchTurnCommand(Models.Json.SearchableTurn Entry) : ICommand<bool>;

public sealed class SelectSearchTurnHandler(IRequestDetailsTarget target) : ICommandHandler<SelectSearchTurnCommand, bool>
{
    public Task<Result<bool>> HandleAsync(SelectSearchTurnCommand command, CallContext context)
    {
        target.SelectSearchTurn(command.Entry);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Clipboard ---

public sealed record CopyTextCommand(string Text) : ICommand<bool>;

public sealed class CopyTextHandler(IClipboardTarget target) : ICommandHandler<CopyTextCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(CopyTextCommand command, CallContext context)
    {
        await target.CopyText(command.Text);
        return Result<bool>.Success(true);
    }
}

public sealed record CopyOriginalJsonCommand(Models.Json.UnifiedSessionTurn? Entry) : ICommand<bool>;

public sealed class CopyOriginalJsonHandler(IClipboardTarget target) : ICommandHandler<CopyOriginalJsonCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(CopyOriginalJsonCommand command, CallContext context)
    {
        await target.CopyOriginalJson(command.Entry);
        return Result<bool>.Success(true);
    }
}

// --- Preview/Markdown ---

public sealed record OpenPreviewInBrowserCommand() : ICommand<bool>;

public sealed class OpenPreviewInBrowserHandler(IPreviewTarget target) : ICommandHandler<OpenPreviewInBrowserCommand, bool>
{
    public Task<Result<bool>> HandleAsync(OpenPreviewInBrowserCommand command, CallContext context)
    {
        target.OpenPreviewInBrowser();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record ToggleShowRawMarkdownCommand() : ICommand<bool>;

public sealed class ToggleShowRawMarkdownHandler(IPreviewTarget target) : ICommandHandler<ToggleShowRawMarkdownCommand, bool>
{
    public Task<Result<bool>> HandleAsync(ToggleShowRawMarkdownCommand command, CallContext context)
    {
        target.ToggleShowRawMarkdown();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Archive ---

public sealed record ArchiveCurrentCommand() : ICommand<bool>;

public sealed class ArchiveCurrentHandler(IArchiveTarget target) : ICommandHandler<ArchiveCurrentCommand, bool>
{
    public Task<Result<bool>> HandleAsync(ArchiveCurrentCommand command, CallContext context)
    {
        target.Archive();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record ArchiveTreeItemCommand(Models.FileNode? Node) : ICommand<bool>;

public sealed class ArchiveTreeItemHandler(IArchiveTarget target) : ICommandHandler<ArchiveTreeItemCommand, bool>
{
    public Task<Result<bool>> HandleAsync(ArchiveTreeItemCommand command, CallContext context)
    {
        target.ArchiveTreeItem(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Tree Operations ---

public sealed record OpenTreeItemCommand(Models.FileNode? Node) : ICommand<bool>;

public sealed class OpenTreeItemHandler(INavigationTarget target) : ICommandHandler<OpenTreeItemCommand, bool>
{
    public Task<Result<bool>> HandleAsync(OpenTreeItemCommand command, CallContext context)
    {
        target.OpenTreeItem(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Config ---

public sealed record OpenAgentConfigCommand() : ICommand<bool>;

public sealed class OpenAgentConfigHandler(IConfigTarget target) : ICommandHandler<OpenAgentConfigCommand, bool>
{
    public Task<Result<bool>> HandleAsync(OpenAgentConfigCommand command, CallContext context)
    {
        target.OpenAgentConfig();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record OpenPromptTemplatesCommand() : ICommand<bool>;

public sealed class OpenPromptTemplatesHandler(IConfigTarget target) : ICommandHandler<OpenPromptTemplatesCommand, bool>
{
    public Task<Result<bool>> HandleAsync(OpenPromptTemplatesCommand command, CallContext context)
    {
        target.OpenPromptTemplates();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Phone Navigation ---

public sealed record PhoneNavigateSectionCommand(string? SectionKey) : ICommand<bool>;

public sealed class PhoneNavigateSectionHandler(INavigationTarget target) : ICommandHandler<PhoneNavigateSectionCommand, bool>
{
    public Task<Result<bool>> HandleAsync(PhoneNavigateSectionCommand command, CallContext context)
    {
        target.PhoneNavigateSection(command.SectionKey);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Tree Item Tap ---

public sealed record TreeItemTappedCommand(Models.FileNode? Node) : ICommand<bool>;

public sealed class TreeItemTappedHandler(INavigationTarget target) : ICommandHandler<TreeItemTappedCommand, bool>
{
    public Task<Result<bool>> HandleAsync(TreeItemTappedCommand command, CallContext context)
    {
        target.TreeItemTapped(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- JSON Node Double-Tap ---

public sealed record JsonNodeDoubleTappedCommand(Models.Json.JsonTreeNode? Node) : ICommand<bool>;

public sealed class JsonNodeDoubleTappedHandler(INavigationTarget target) : ICommandHandler<JsonNodeDoubleTappedCommand, bool>
{
    public Task<Result<bool>> HandleAsync(JsonNodeDoubleTappedCommand command, CallContext context)
    {
        target.JsonNodeDoubleTapped(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Search Row Tap ---

public sealed record SearchRowTappedCommand(Models.Json.SearchableTurn? Entry) : ICommand<bool>;

public sealed class SearchRowTappedHandler(IRequestDetailsTarget target) : ICommandHandler<SearchRowTappedCommand, bool>
{
    public Task<Result<bool>> HandleAsync(SearchRowTappedCommand command, CallContext context)
    {
        target.SearchRowTapped(command.Entry);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Search Row Double-Tap ---

public sealed record SearchRowDoubleTappedCommand(Models.Json.SearchableTurn? Entry) : ICommand<bool>;

public sealed class SearchRowDoubleTappedHandler(IRequestDetailsTarget target) : ICommandHandler<SearchRowDoubleTappedCommand, bool>
{
    public Task<Result<bool>> HandleAsync(SearchRowDoubleTappedCommand command, CallContext context)
    {
        target.SearchRowDoubleTapped(command.Entry);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Workspace Switch (C4 MainWindow thin per PLAN-VM-CQRS-REMEDIATION-001) ---

public sealed record SwitchWorkspaceConnectionCommand(WorkspaceConnectionOption Option) : ICommand<bool>;

public sealed class SwitchWorkspaceConnectionHandler(IWorkspaceSwitchTarget target) : ICommandHandler<SwitchWorkspaceConnectionCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SwitchWorkspaceConnectionCommand command, CallContext context)
    {
        await target.SwitchWorkspaceConnectionAsync(command.Option);
        return Result<bool>.Success(true);
    }
}

// --- Load Workspace Connections (C4 MainWindow thin per PLAN-VM-CQRS-REMEDIATION-001 / PLAN-C4-MAINWINDOW-001) ---
// Command contract defined first (tests-first per Byrd). Handler and VM dispatch follow.
public sealed record LoadWorkspaceConnectionsCommand(
    WorkspaceConnectionOption? PreferredSelection,
    string PreferredBaseUrl,
    bool SuppressStatusFailure) : ICommand<bool>;

public sealed class LoadWorkspaceConnectionsHandler(ILoadWorkspaceConnectionsTarget target) : ICommandHandler<LoadWorkspaceConnectionsCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(LoadWorkspaceConnectionsCommand command, CallContext context)
    {
        await target.LoadWorkspaceConnectionsAsync(command.PreferredSelection, command.PreferredBaseUrl, command.SuppressStatusFailure);
        return Result<bool>.Success(true);
    }
}

// --- Workspace Health (remaining MainWindow thin slice per PLAN-VM-CQRS-REMEDIATION-001) ---
// Tests-first: command defined, handler bridges to target (VM implements thin dispatch).
public sealed record RefreshSelectedWorkspaceHealthCommand(
    string? SelectedBaseUrl = null,
    string? DisplayName = null) : ICommand<bool>;

public sealed class RefreshSelectedWorkspaceHealthHandler(
    IWorkspaceHealthTarget target,
    IUiDispatcherService uiDispatcher) : ICommandHandler<RefreshSelectedWorkspaceHealthCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RefreshSelectedWorkspaceHealthCommand command, CallContext context)
    {
        // Real handler logic (moved from MainWindowViewModel Core per PLAN-VM-CQRS-REMEDIATION-001).
        // VM entry remains thin dispatch+Apply. 
        var baseUrl = command.SelectedBaseUrl;
        var displayName = command.DisplayName ?? "workspace";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            target.UpdateWorkspaceHealthIndicator(null, "Select a workspace");
            return Result<bool>.Success(true);
        }

        // Simple normalize (dupe of VM private to keep handler independent for now)
        baseUrl = baseUrl.Trim().TrimEnd('/');

        try
        {
            // Use static as defined on the service (no instance state required for probe).
            var health = await McpWorkspaceService.ProbeHealthAsync(baseUrl).ConfigureAwait(true);
            target.UpdateWorkspaceHealthIndicator(health.Success, FormatTooltip(displayName, health));
        }
        catch (Exception ex)
        {
            target.UpdateWorkspaceHealthIndicator(false, $"Unhealthy: {displayName} ({ex.Message})");
        }

        return Result<bool>.Success(true);
    }

    private static string FormatTooltip(string displayName, McpWorkspaceHealthResult health)
    {
        var status = health.StatusCode > 0 ? $"HTTP {health.StatusCode}" : "HTTP n/a";
        var endpoint = string.IsNullOrWhiteSpace(health.Url) ? "" : $" @ {health.Url}";
        var error = string.IsNullOrWhiteSpace(health.Error) ? "" : $" ({health.Error})";
        return $"{(health.Success ? "Healthy" : "Unhealthy")}: {displayName} - {status}{endpoint}{error}";
    }
}


