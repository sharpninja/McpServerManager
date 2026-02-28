using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Cqrs;

namespace McpServerManager.Core.Commands;

// --- Navigation Commands ---

public sealed class NavigateBackCommand : ICommand
{
    public ICommandTarget Target { get; }
    public NavigateBackCommand(ICommandTarget target) => Target = target;
}

public sealed class NavigateBackHandler : ICommandHandler<NavigateBackCommand>
{
    public Task ExecuteAsync(NavigateBackCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.NavigateBack();
        return Task.CompletedTask;
    }
}

public sealed class NavigateForwardCommand : ICommand
{
    public ICommandTarget Target { get; }
    public NavigateForwardCommand(ICommandTarget target) => Target = target;
}

public sealed class NavigateForwardHandler : ICommandHandler<NavigateForwardCommand>
{
    public Task ExecuteAsync(NavigateForwardCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.NavigateForward();
        return Task.CompletedTask;
    }
}

// --- Refresh ---

public sealed class RefreshViewCommand : ICommand
{
    public ICommandTarget Target { get; }
    public RefreshViewCommand(ICommandTarget target) => Target = target;
}

public sealed class RefreshViewHandler : ICommandHandler<RefreshViewCommand>
{
    public async Task ExecuteAsync(RefreshViewCommand command, CancellationToken cancellationToken = default)
    {
        await command.Target.RefreshAsync();
    }
}

// --- Request Details ---

public sealed class ShowRequestDetailsCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.SearchableEntry Entry { get; }
    public ShowRequestDetailsCommand(ICommandTarget target, Models.Json.SearchableEntry entry)
    {
        Target = target;
        Entry = entry;
    }
}

public sealed class ShowRequestDetailsHandler : ICommandHandler<ShowRequestDetailsCommand>
{
    public Task ExecuteAsync(ShowRequestDetailsCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.ShowRequestDetails(command.Entry);
        return Task.CompletedTask;
    }
}

public sealed class CloseRequestDetailsCommand : ICommand
{
    public ICommandTarget Target { get; }
    public CloseRequestDetailsCommand(ICommandTarget target) => Target = target;
}

public sealed class CloseRequestDetailsHandler : ICommandHandler<CloseRequestDetailsCommand>
{
    public Task ExecuteAsync(CloseRequestDetailsCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.CloseRequestDetails();
        return Task.CompletedTask;
    }
}

public sealed class NavigateToPreviousRequestCommand : ICommand
{
    public ICommandTarget Target { get; }
    public NavigateToPreviousRequestCommand(ICommandTarget target) => Target = target;
}

public sealed class NavigateToPreviousRequestHandler : ICommandHandler<NavigateToPreviousRequestCommand>
{
    public Task ExecuteAsync(NavigateToPreviousRequestCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.NavigateToPreviousRequest();
        return Task.CompletedTask;
    }
}

public sealed class NavigateToNextRequestCommand : ICommand
{
    public ICommandTarget Target { get; }
    public NavigateToNextRequestCommand(ICommandTarget target) => Target = target;
}

public sealed class NavigateToNextRequestHandler : ICommandHandler<NavigateToNextRequestCommand>
{
    public Task ExecuteAsync(NavigateToNextRequestCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.NavigateToNextRequest();
        return Task.CompletedTask;
    }
}

// --- Selection ---

public sealed class SelectSearchEntryCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.SearchableEntry Entry { get; }
    public SelectSearchEntryCommand(ICommandTarget target, Models.Json.SearchableEntry entry)
    {
        Target = target;
        Entry = entry;
    }
}

public sealed class SelectSearchEntryHandler : ICommandHandler<SelectSearchEntryCommand>
{
    public Task ExecuteAsync(SelectSearchEntryCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.SelectSearchEntry(command.Entry);
        return Task.CompletedTask;
    }
}

// --- Clipboard ---

public sealed class CopyTextCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string Text { get; }
    public CopyTextCommand(ICommandTarget target, string text)
    {
        Target = target;
        Text = text;
    }
}

public sealed class CopyTextHandler : ICommandHandler<CopyTextCommand>
{
    public async Task ExecuteAsync(CopyTextCommand command, CancellationToken cancellationToken = default)
    {
        await command.Target.CopyText(command.Text);
    }
}

public sealed class CopyOriginalJsonCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.UnifiedRequestEntry? Entry { get; }
    public CopyOriginalJsonCommand(ICommandTarget target, Models.Json.UnifiedRequestEntry? entry)
    {
        Target = target;
        Entry = entry;
    }
}

public sealed class CopyOriginalJsonHandler : ICommandHandler<CopyOriginalJsonCommand>
{
    public async Task ExecuteAsync(CopyOriginalJsonCommand command, CancellationToken cancellationToken = default)
    {
        await command.Target.CopyOriginalJson(command.Entry);
    }
}

// --- Preview/Markdown ---

public sealed class OpenPreviewInBrowserCommand : ICommand
{
    public ICommandTarget Target { get; }
    public OpenPreviewInBrowserCommand(ICommandTarget target) => Target = target;
}

public sealed class OpenPreviewInBrowserHandler : ICommandHandler<OpenPreviewInBrowserCommand>
{
    public Task ExecuteAsync(OpenPreviewInBrowserCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.OpenPreviewInBrowser();
        return Task.CompletedTask;
    }
}

public sealed class ToggleShowRawMarkdownCommand : ICommand
{
    public ICommandTarget Target { get; }
    public ToggleShowRawMarkdownCommand(ICommandTarget target) => Target = target;
}

public sealed class ToggleShowRawMarkdownHandler : ICommandHandler<ToggleShowRawMarkdownCommand>
{
    public Task ExecuteAsync(ToggleShowRawMarkdownCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.ToggleShowRawMarkdown();
        return Task.CompletedTask;
    }
}

// --- Archive ---

public sealed class ArchiveCurrentCommand : ICommand
{
    public ICommandTarget Target { get; }
    public ArchiveCurrentCommand(ICommandTarget target) => Target = target;
}

public sealed class ArchiveCurrentHandler : ICommandHandler<ArchiveCurrentCommand>
{
    public Task ExecuteAsync(ArchiveCurrentCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.Archive();
        return Task.CompletedTask;
    }
}

public sealed class ArchiveTreeItemCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.FileNode? Node { get; }
    public ArchiveTreeItemCommand(ICommandTarget target, Models.FileNode? node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class ArchiveTreeItemHandler : ICommandHandler<ArchiveTreeItemCommand>
{
    public Task ExecuteAsync(ArchiveTreeItemCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.ArchiveTreeItem(command.Node);
        return Task.CompletedTask;
    }
}

// --- Tree Operations ---

public sealed class OpenTreeItemCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.FileNode? Node { get; }
    public OpenTreeItemCommand(ICommandTarget target, Models.FileNode? node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class OpenTreeItemHandler : ICommandHandler<OpenTreeItemCommand>
{
    public Task ExecuteAsync(OpenTreeItemCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.OpenTreeItem(command.Node);
        return Task.CompletedTask;
    }
}

// --- Config ---

public sealed class OpenAgentConfigCommand : ICommand
{
    public ICommandTarget Target { get; }
    public OpenAgentConfigCommand(ICommandTarget target) => Target = target;
}

public sealed class OpenAgentConfigHandler : ICommandHandler<OpenAgentConfigCommand>
{
    public Task ExecuteAsync(OpenAgentConfigCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.OpenAgentConfig();
        return Task.CompletedTask;
    }
}

public sealed class OpenPromptTemplatesCommand : ICommand
{
    public ICommandTarget Target { get; }
    public OpenPromptTemplatesCommand(ICommandTarget target) => Target = target;
}

public sealed class OpenPromptTemplatesHandler : ICommandHandler<OpenPromptTemplatesCommand>
{
    public Task ExecuteAsync(OpenPromptTemplatesCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.OpenPromptTemplates();
        return Task.CompletedTask;
    }
}

// --- Phone Navigation ---

public sealed class PhoneNavigateSectionCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string? SectionKey { get; }
    public PhoneNavigateSectionCommand(ICommandTarget target, string? sectionKey)
    {
        Target = target;
        SectionKey = sectionKey;
    }
}

public sealed class PhoneNavigateSectionHandler : ICommandHandler<PhoneNavigateSectionCommand>
{
    public Task ExecuteAsync(PhoneNavigateSectionCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.PhoneNavigateSection(command.SectionKey);
        return Task.CompletedTask;
    }
}

// --- Tree Item Tap ---

public sealed class TreeItemTappedCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.FileNode? Node { get; }
    public TreeItemTappedCommand(ICommandTarget target, Models.FileNode? node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class TreeItemTappedHandler : ICommandHandler<TreeItemTappedCommand>
{
    public Task ExecuteAsync(TreeItemTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.TreeItemTapped(command.Node);
        return Task.CompletedTask;
    }
}

// --- JSON Node Double-Tap ---

public sealed class JsonNodeDoubleTappedCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.JsonTreeNode? Node { get; }
    public JsonNodeDoubleTappedCommand(ICommandTarget target, Models.Json.JsonTreeNode? node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class JsonNodeDoubleTappedHandler : ICommandHandler<JsonNodeDoubleTappedCommand>
{
    public Task ExecuteAsync(JsonNodeDoubleTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.JsonNodeDoubleTapped(command.Node);
        return Task.CompletedTask;
    }
}

// --- Search Row Tap ---

public sealed class SearchRowTappedCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.SearchableEntry? Entry { get; }
    public SearchRowTappedCommand(ICommandTarget target, Models.Json.SearchableEntry? entry)
    {
        Target = target;
        Entry = entry;
    }
}

public sealed class SearchRowTappedHandler : ICommandHandler<SearchRowTappedCommand>
{
    public Task ExecuteAsync(SearchRowTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.SearchRowTapped(command.Entry);
        return Task.CompletedTask;
    }
}

// --- Search Row Double-Tap ---

public sealed class SearchRowDoubleTappedCommand : ICommand
{
    public ICommandTarget Target { get; }
    public Models.Json.SearchableEntry? Entry { get; }
    public SearchRowDoubleTappedCommand(ICommandTarget target, Models.Json.SearchableEntry? entry)
    {
        Target = target;
        Entry = entry;
    }
}

public sealed class SearchRowDoubleTappedHandler : ICommandHandler<SearchRowDoubleTappedCommand>
{
    public Task ExecuteAsync(SearchRowDoubleTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.SearchRowDoubleTapped(command.Entry);
        return Task.CompletedTask;
    }
}
