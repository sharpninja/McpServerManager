using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServer.Cqrs;
using McpServer.Cqrs.Mvvm;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// ViewModel for the Memory tab list/grid.
/// </summary>
[ViewModelCommand("list-memories", Description = "List workspace memories")]
public sealed partial class MemoryListViewModel : AreaListViewModelBase<MemoryListItem>
{
    private readonly CqrsQueryCommand<ListMemoriesResult> _refreshCommand;
    private readonly ILogger<MemoryListViewModel> _logger;

    /// <summary>Initializes a new instance of the memory list ViewModel.</summary>
    public MemoryListViewModel(
        Dispatcher dispatcher,
        WorkspaceContextViewModel workspaceContext,
        ILogger<MemoryListViewModel> logger)
        : base(McpArea.Memory)
    {
        _logger = logger;
        _refreshCommand = new CqrsQueryCommand<ListMemoriesResult>(dispatcher, BuildQuery);
        workspaceContext.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceContextViewModel.ActiveWorkspacePath))
                _ = Task.Run(() => LoadAsync());
        };
    }

    /// <summary>Optional scope filter. Null = Effective.</summary>
    [ObservableProperty]
    private MemoryScope? _scope;

    /// <summary>Optional category filter.</summary>
    [ObservableProperty]
    private string? _category;

    /// <summary>Optional keyword filter.</summary>
    [ObservableProperty]
    private string? _keyword;

    /// <summary>Refresh command (also the primary command for exec).</summary>
    public IAsyncRelayCommand RefreshCommand => _refreshCommand;

    /// <summary>Primary command alias for registry execution.</summary>
    public IAsyncRelayCommand PrimaryCommand => RefreshCommand;

    /// <summary>Last query result.</summary>
    public Result<ListMemoriesResult>? LastResult => _refreshCommand.LastResult;

    /// <summary>Loads memories into the list.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading memories...";

        try
        {
            var result = await _refreshCommand.DispatchAsync(ct).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Error ?? "Unknown error loading memories.";
                StatusMessage = "Memory load failed.";
                return;
            }

            SetItems(result.Value.Items, result.Value.TotalCount);
            StatusMessage = $"Loaded {Items.Count} memories.";
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Memory load failed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private ListMemoriesQuery BuildQuery() => new()
    {
        Scope = Scope,
        Category = NormalizeFilter(Category),
        Keyword = NormalizeFilter(Keyword),
    };

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
