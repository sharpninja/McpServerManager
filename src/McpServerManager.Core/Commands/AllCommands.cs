using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Core.Commands;

// --- Navigation Commands ---

public sealed class NavigateBackCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public NavigateBackCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class NavigateBackHandler : ICommandHandler<NavigateBackCommand>
{
    public Task ExecuteAsync(NavigateBackCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.NavigateBackInternal();
        return Task.CompletedTask;
    }
}

public sealed class NavigateForwardCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public NavigateForwardCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class NavigateForwardHandler : ICommandHandler<NavigateForwardCommand>
{
    public Task ExecuteAsync(NavigateForwardCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.NavigateForwardInternal();
        return Task.CompletedTask;
    }
}

// --- Refresh ---

public sealed class RefreshViewCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public RefreshViewCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class RefreshViewHandler : ICommandHandler<RefreshViewCommand>
{
    public async Task ExecuteAsync(RefreshViewCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.RefreshInternalAsync();
    }
}

// --- Request Details ---

public sealed class ShowRequestDetailsCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.SearchableEntry Entry { get; }
    public ShowRequestDetailsCommand(MainWindowViewModel vm, Models.Json.SearchableEntry entry)
    {
        ViewModel = vm;
        Entry = entry;
    }
}

public sealed class ShowRequestDetailsHandler : ICommandHandler<ShowRequestDetailsCommand>
{
    public Task ExecuteAsync(ShowRequestDetailsCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.ShowRequestDetailsInternal(command.Entry);
        return Task.CompletedTask;
    }
}

public sealed class CloseRequestDetailsCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public CloseRequestDetailsCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class CloseRequestDetailsHandler : ICommandHandler<CloseRequestDetailsCommand>
{
    public Task ExecuteAsync(CloseRequestDetailsCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.CloseRequestDetailsInternal();
        return Task.CompletedTask;
    }
}

public sealed class NavigateToPreviousRequestCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public NavigateToPreviousRequestCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class NavigateToPreviousRequestHandler : ICommandHandler<NavigateToPreviousRequestCommand>
{
    public Task ExecuteAsync(NavigateToPreviousRequestCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.NavigateToPreviousRequestInternal();
        return Task.CompletedTask;
    }
}

public sealed class NavigateToNextRequestCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public NavigateToNextRequestCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class NavigateToNextRequestHandler : ICommandHandler<NavigateToNextRequestCommand>
{
    public Task ExecuteAsync(NavigateToNextRequestCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.NavigateToNextRequestInternal();
        return Task.CompletedTask;
    }
}

// --- Selection ---

public sealed class SelectSearchEntryCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.SearchableEntry Entry { get; }
    public SelectSearchEntryCommand(MainWindowViewModel vm, Models.Json.SearchableEntry entry)
    {
        ViewModel = vm;
        Entry = entry;
    }
}

public sealed class SelectSearchEntryHandler : ICommandHandler<SelectSearchEntryCommand>
{
    public Task ExecuteAsync(SelectSearchEntryCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.SelectSearchEntryInternal(command.Entry);
        return Task.CompletedTask;
    }
}

// --- Clipboard ---

public sealed class CopyTextCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string Text { get; }
    public CopyTextCommand(MainWindowViewModel vm, string text)
    {
        ViewModel = vm;
        Text = text;
    }
}

public sealed class CopyTextHandler : ICommandHandler<CopyTextCommand>
{
    public async Task ExecuteAsync(CopyTextCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.CopyTextInternal(command.Text);
    }
}

public sealed class CopyOriginalJsonCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.UnifiedRequestEntry? Entry { get; }
    public CopyOriginalJsonCommand(MainWindowViewModel vm, Models.Json.UnifiedRequestEntry? entry)
    {
        ViewModel = vm;
        Entry = entry;
    }
}

public sealed class CopyOriginalJsonHandler : ICommandHandler<CopyOriginalJsonCommand>
{
    public async Task ExecuteAsync(CopyOriginalJsonCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.CopyOriginalJsonInternal(command.Entry);
    }
}

// --- Preview/Markdown ---

public sealed class OpenPreviewInBrowserCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public OpenPreviewInBrowserCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class OpenPreviewInBrowserHandler : ICommandHandler<OpenPreviewInBrowserCommand>
{
    public Task ExecuteAsync(OpenPreviewInBrowserCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenPreviewInBrowserInternal();
        return Task.CompletedTask;
    }
}

public sealed class ToggleShowRawMarkdownCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public ToggleShowRawMarkdownCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class ToggleShowRawMarkdownHandler : ICommandHandler<ToggleShowRawMarkdownCommand>
{
    public Task ExecuteAsync(ToggleShowRawMarkdownCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.ToggleShowRawMarkdownInternal();
        return Task.CompletedTask;
    }
}

// --- Archive ---

public sealed class ArchiveCurrentCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public ArchiveCurrentCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class ArchiveCurrentHandler : ICommandHandler<ArchiveCurrentCommand>
{
    public Task ExecuteAsync(ArchiveCurrentCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.ArchiveInternal();
        return Task.CompletedTask;
    }
}

public sealed class ArchiveTreeItemCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.FileNode? Node { get; }
    public ArchiveTreeItemCommand(MainWindowViewModel vm, Models.FileNode? node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class ArchiveTreeItemHandler : ICommandHandler<ArchiveTreeItemCommand>
{
    public Task ExecuteAsync(ArchiveTreeItemCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.ArchiveTreeItemInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- Tree Operations ---

public sealed class OpenTreeItemCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.FileNode? Node { get; }
    public OpenTreeItemCommand(MainWindowViewModel vm, Models.FileNode? node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class OpenTreeItemHandler : ICommandHandler<OpenTreeItemCommand>
{
    public Task ExecuteAsync(OpenTreeItemCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenTreeItemInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- Config ---

public sealed class OpenAgentConfigCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public OpenAgentConfigCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class OpenAgentConfigHandler : ICommandHandler<OpenAgentConfigCommand>
{
    public Task ExecuteAsync(OpenAgentConfigCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenAgentConfigInternal();
        return Task.CompletedTask;
    }
}

public sealed class OpenPromptTemplatesCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public OpenPromptTemplatesCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class OpenPromptTemplatesHandler : ICommandHandler<OpenPromptTemplatesCommand>
{
    public Task ExecuteAsync(OpenPromptTemplatesCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenPromptTemplatesInternal();
        return Task.CompletedTask;
    }
}

// --- Phone Navigation ---

public sealed class PhoneNavigateSectionCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string? SectionKey { get; }
    public PhoneNavigateSectionCommand(MainWindowViewModel vm, string? sectionKey)
    {
        ViewModel = vm;
        SectionKey = sectionKey;
    }
}

public sealed class PhoneNavigateSectionHandler : ICommandHandler<PhoneNavigateSectionCommand>
{
    public Task ExecuteAsync(PhoneNavigateSectionCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.PhoneNavigateSectionInternal(command.SectionKey);
        return Task.CompletedTask;
    }
}

// --- Tree Item Tap ---

public sealed class TreeItemTappedCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.FileNode? Node { get; }
    public TreeItemTappedCommand(MainWindowViewModel vm, Models.FileNode? node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class TreeItemTappedHandler : ICommandHandler<TreeItemTappedCommand>
{
    public Task ExecuteAsync(TreeItemTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.TreeItemTappedInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- JSON Node Double-Tap ---

public sealed class JsonNodeDoubleTappedCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.JsonTreeNode? Node { get; }
    public JsonNodeDoubleTappedCommand(MainWindowViewModel vm, Models.Json.JsonTreeNode? node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class JsonNodeDoubleTappedHandler : ICommandHandler<JsonNodeDoubleTappedCommand>
{
    public Task ExecuteAsync(JsonNodeDoubleTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.JsonNodeDoubleTappedInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- Search Row Tap ---

public sealed class SearchRowTappedCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.SearchableEntry? Entry { get; }
    public SearchRowTappedCommand(MainWindowViewModel vm, Models.Json.SearchableEntry? entry)
    {
        ViewModel = vm;
        Entry = entry;
    }
}

public sealed class SearchRowTappedHandler : ICommandHandler<SearchRowTappedCommand>
{
    public Task ExecuteAsync(SearchRowTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.SearchRowTappedInternal(command.Entry);
        return Task.CompletedTask;
    }
}

// --- Search Row Double-Tap ---

public sealed class SearchRowDoubleTappedCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public Models.Json.SearchableEntry? Entry { get; }
    public SearchRowDoubleTappedCommand(MainWindowViewModel vm, Models.Json.SearchableEntry? entry)
    {
        ViewModel = vm;
        Entry = entry;
    }
}

public sealed class SearchRowDoubleTappedHandler : ICommandHandler<SearchRowDoubleTappedCommand>
{
    public Task ExecuteAsync(SearchRowDoubleTappedCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.SearchRowDoubleTappedInternal(command.Entry);
        return Task.CompletedTask;
    }
}
