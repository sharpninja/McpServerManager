using System;
using McpServerManager.Core.Cqrs;

namespace McpServerManager.Core.Services;

/// <summary>
/// Factory for creating and registering all app-side CQRS mediator handlers.
/// Extracts handler registration from MainWindowViewModel to the composition root.
/// </summary>
public static class AppMediatorFactory
{
    /// <summary>
    /// Creates a new <see cref="Mediator"/> and registers all command/query handlers.
    /// </summary>
    /// <param name="onBusyChanged">Optional callback invoked when the mediator's IsBusy state changes.</param>
    public static Mediator CreateAndRegisterAllHandlers(Action<bool>? onBusyChanged = null)
    {
        var mediator = new Mediator();

        if (onBusyChanged is not null)
            mediator.IsBusyChanged += onBusyChanged;

        // Async operations (data loading)
        mediator.Register(new Commands.InitializeFromMcpHandler());
        mediator.Register(new Commands.RefreshAndLoadAllJsonHandler());
        mediator.Register(new Commands.RefreshAndLoadAgentJsonHandler());
        mediator.Register(new Commands.RefreshAndLoadSessionHandler());
        mediator.Register(new Commands.LoadJsonFileHandler());
        mediator.Register(new Commands.NavigateToNodeHandler());
        mediator.Register(new Commands.LoadMarkdownFileHandler());
        mediator.Register(new Commands.LoadSourceFileHandler());

        // Navigation
        mediator.Register(new Commands.NavigateBackHandler());
        mediator.Register(new Commands.NavigateForwardHandler());
        mediator.Register(new Commands.RefreshViewHandler());
        mediator.Register(new Commands.PhoneNavigateSectionHandler());
        mediator.Register(new Commands.TreeItemTappedHandler());

        // Request details
        mediator.Register(new Commands.ShowRequestDetailsHandler());
        mediator.Register(new Commands.CloseRequestDetailsHandler());
        mediator.Register(new Commands.NavigateToPreviousRequestHandler());
        mediator.Register(new Commands.NavigateToNextRequestHandler());

        // Selection & interaction
        mediator.Register(new Commands.SelectSearchEntryHandler());
        mediator.Register(new Commands.JsonNodeDoubleTappedHandler());
        mediator.Register(new Commands.SearchRowTappedHandler());
        mediator.Register(new Commands.SearchRowDoubleTappedHandler());

        // Clipboard
        mediator.Register(new Commands.CopyTextHandler());
        mediator.Register(new Commands.CopyOriginalJsonHandler());

        // Preview/Markdown
        mediator.Register(new Commands.OpenPreviewInBrowserHandler());
        mediator.Register(new Commands.ToggleShowRawMarkdownHandler());

        // Archive
        mediator.Register(new Commands.ArchiveCurrentHandler());
        mediator.Register(new Commands.ArchiveTreeItemHandler());

        // Tree & config
        mediator.Register(new Commands.OpenTreeItemHandler());
        mediator.Register(new Commands.OpenAgentConfigHandler());
        mediator.Register(new Commands.OpenPromptTemplatesHandler());

        // Refresh
        mediator.Register(new Commands.RefreshHandler());

        return mediator;
    }
}
