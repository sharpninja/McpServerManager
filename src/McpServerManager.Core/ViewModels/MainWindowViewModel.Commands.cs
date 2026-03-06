using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;
using McpServer.UI.Core.Models;
using McpServer.UI.Core.Models.Json;

namespace McpServerManager.Core.ViewModels;

public partial class MainWindowViewModel
{
    private CqrsRelayCommand<bool>? _phoneNavigateSectionCommand;
    public CqrsRelayCommand<bool> PhoneNavigateSectionCommand => _phoneNavigateSectionCommand ??=
        CqrsRelayFactory.Create<string?>(_dispatcher, PhoneNavigateSection);

    private CqrsRelayCommand<bool>? _treeItemTappedCommand;
    public CqrsRelayCommand<bool> TreeItemTappedCommand => _treeItemTappedCommand ??=
        CqrsRelayFactory.Create<FileNode?>(_dispatcher, TreeItemTapped);

    private CqrsRelayCommand<bool>? _jsonNodeDoubleTappedCommand;
    public CqrsRelayCommand<bool> JsonNodeDoubleTappedCommand => _jsonNodeDoubleTappedCommand ??=
        CqrsRelayFactory.Create<JsonTreeNode?>(_dispatcher, JsonNodeDoubleTapped);

    private CqrsRelayCommand<bool>? _searchRowTappedCommand;
    public CqrsRelayCommand<bool> SearchRowTappedCommand => _searchRowTappedCommand ??=
        CqrsRelayFactory.Create<SearchableEntry?>(_dispatcher, SearchRowTapped);

    private CqrsRelayCommand<bool>? _searchRowDoubleTappedCommand;
    public CqrsRelayCommand<bool> SearchRowDoubleTappedCommand => _searchRowDoubleTappedCommand ??=
        CqrsRelayFactory.Create<SearchableEntry?>(_dispatcher, SearchRowDoubleTapped);

    private CqrsRelayCommand<bool>? _loadWorkspaceConnectionsCommand;
    public CqrsRelayCommand<bool> LoadWorkspaceConnectionsCommand => _loadWorkspaceConnectionsCommand ??=
        CqrsRelayFactory.Create(_dispatcher, LoadWorkspaceConnectionsAsync);

    private CqrsRelayCommand<bool>? _navigateBackCommand;
    public CqrsRelayCommand<bool> NavigateBackCommand => _navigateBackCommand ??=
        CqrsRelayFactory.Create(_dispatcher, NavigateBack, CanNavigateBack);

    private CqrsRelayCommand<bool>? _navigateForwardCommand;
    public CqrsRelayCommand<bool> NavigateForwardCommand => _navigateForwardCommand ??=
        CqrsRelayFactory.Create(_dispatcher, NavigateForward, CanNavigateForward);

    private CqrsRelayCommand<bool>? _refreshCommand;
    public CqrsRelayCommand<bool> RefreshCommand => _refreshCommand ??=
        CqrsRelayFactory.Create(_dispatcher, RefreshAsync, CanRefresh);

    private CqrsRelayCommand<bool>? _showRequestDetailsCommand;
    public CqrsRelayCommand<bool> ShowRequestDetailsCommand => _showRequestDetailsCommand ??=
        CqrsRelayFactory.Create<SearchableEntry?>(_dispatcher, entry =>
        {
            if (entry is not null)
                ShowRequestDetails(entry);
        });

    private CqrsRelayCommand<bool>? _openChatWindowCommand;
    public CqrsRelayCommand<bool> OpenChatWindowCommand => _openChatWindowCommand ??=
        CqrsRelayFactory.Create(_dispatcher, OpenChatWindow);

    private CqrsRelayCommand<bool>? _logoutCommand;
    public CqrsRelayCommand<bool> LogoutCommand => _logoutCommand ??=
        CqrsRelayFactory.Create(_dispatcher, Logout);

    private CqrsRelayCommand<bool>? _selectSearchEntryCommand;
    public CqrsRelayCommand<bool> SelectSearchEntryCommand => _selectSearchEntryCommand ??=
        CqrsRelayFactory.Create<SearchableEntry?>(_dispatcher, entry =>
        {
            if (entry is not null)
                SelectSearchEntry(entry);
        });

    private CqrsRelayCommand<bool>? _navigateToPreviousRequestCommand;
    public CqrsRelayCommand<bool> NavigateToPreviousRequestCommand => _navigateToPreviousRequestCommand ??=
        CqrsRelayFactory.Create(_dispatcher, NavigateToPreviousRequest, CanNavigateToPreviousRequest);

    private CqrsRelayCommand<bool>? _navigateToNextRequestCommand;
    public CqrsRelayCommand<bool> NavigateToNextRequestCommand => _navigateToNextRequestCommand ??=
        CqrsRelayFactory.Create(_dispatcher, NavigateToNextRequest, CanNavigateToNextRequest);

    private CqrsRelayCommand<bool>? _copyTextCommand;
    public CqrsRelayCommand<bool> CopyTextCommand => _copyTextCommand ??=
        CqrsRelayFactory.Create<string?>(_dispatcher, async text =>
            await CopyText(text ?? string.Empty).ConfigureAwait(true));

    private CqrsRelayCommand<bool>? _copyOriginalJsonCommand;
    public CqrsRelayCommand<bool> CopyOriginalJsonCommand => _copyOriginalJsonCommand ??=
        CqrsRelayFactory.Create<UnifiedRequestEntry?>(_dispatcher, CopyOriginalJson);

    private CqrsRelayCommand<bool>? _archiveCommand;
    public CqrsRelayCommand<bool> ArchiveCommand => _archiveCommand ??=
        CqrsRelayFactory.Create(_dispatcher, Archive, CanArchive);

    private CqrsRelayCommand<bool>? _openTreeItemCommand;
    public CqrsRelayCommand<bool> OpenTreeItemCommand => _openTreeItemCommand ??=
        CqrsRelayFactory.Create<FileNode?>(_dispatcher, OpenTreeItem);

    private CqrsRelayCommand<bool>? _archiveTreeItemCommand;
    public CqrsRelayCommand<bool> ArchiveTreeItemCommand => _archiveTreeItemCommand ??=
        CqrsRelayFactory.Create<FileNode?>(_dispatcher, ArchiveTreeItem, CanArchiveTreeItem);
}

