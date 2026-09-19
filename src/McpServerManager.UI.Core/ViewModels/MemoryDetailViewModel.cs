using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// ViewModel for viewing, adding, updating, and removing a single memory.
/// Version is displayed but never sent as expectedVersion (D11).
/// </summary>
[ViewModelCommand("get-memory", Description = "Get memory detail")]
public sealed partial class MemoryDetailViewModel : AreaDetailViewModelBase<MemoryDetail>
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<MemoryDetailViewModel> _logger;

    /// <summary>Whether the current editor state represents a new unsaved draft.</summary>
    [ObservableProperty] private bool _isNewDraft = true;

    /// <summary>Editor field for memory ID.</summary>
    [ObservableProperty] private string _editorId = "";

    /// <summary>Editor field for category.</summary>
    [ObservableProperty] private string _editorCategory = "";

    /// <summary>Editor field for scope.</summary>
    [ObservableProperty] private MemoryScope _editorScope = MemoryScope.Workspace;

    /// <summary>Editor field for raw text.</summary>
    [ObservableProperty] private string _editorText = "";

    /// <summary>Editor field for updater name.</summary>
    [ObservableProperty] private string _editorUpdatedBy = "";

    /// <summary>Initializes a new instance of the memory detail ViewModel.</summary>
    public MemoryDetailViewModel(Dispatcher dispatcher, ILogger<MemoryDetailViewModel> logger)
        : base(McpArea.Memory)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>Display-only version from the last loaded/saved detail.</summary>
    public int? DisplayVersion => Detail?.Version;

    /// <summary>Loads a memory by ID.</summary>
    public async Task LoadAsync(string memoryId, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Loading memory...";

        try
        {
            var result = await _dispatcher
                .QueryAsync(new GetMemoryQuery(memoryId), ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error ?? "Unknown error loading memory.";
                StatusMessage = "Memory load failed.";
                return;
            }

            Detail = result.Value;
            LastUpdatedAt = DateTimeOffset.UtcNow;
            if (result.Value is null)
            {
                StatusMessage = "Memory not found.";
                return;
            }

            PopulateEditorFromDetail();
            StatusMessage = $"Loaded '{result.Value.Id}'.";
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Memory load failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Removes the currently loaded memory.</summary>
    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (Detail is null)
            return false;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _dispatcher
                .SendAsync(new RemoveMemoryCommand { MemoryId = Detail.Id }, ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess || result.Value is null || !result.Value.Success)
            {
                ErrorMessage = result.Error ?? result.Value?.Error ?? "Remove failed.";
                return false;
            }

            StatusMessage = $"Removed '{Detail.Id}'.";
            BeginNewDraft();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Adds or updates a memory depending on draft state. Does not send expectedVersion.</summary>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Saving memory...";

        try
        {
            if (IsNewDraft)
            {
                var cmd = new AddMemoryCommand
                {
                    Id = string.IsNullOrWhiteSpace(EditorId) ? null : EditorId.Trim(),
                    Category = EditorCategory,
                    Scope = EditorScope,
                    Text = EditorText,
                    UpdatedBy = string.IsNullOrWhiteSpace(EditorUpdatedBy) ? null : EditorUpdatedBy.Trim(),
                };

                var result = await _dispatcher.SendAsync(cmd, ct).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null || !result.Value.Success)
                {
                    ErrorMessage = result.Error ?? result.Value?.Error ?? "Add failed.";
                    StatusMessage = "Memory add failed.";
                    return false;
                }

                Detail = result.Value.Item;
                IsNewDraft = false;
                if (Detail is not null)
                    PopulateEditorFromDetail();
                StatusMessage = $"Added '{Detail?.Id ?? EditorId}'.";
                return true;
            }

            var update = new UpdateMemoryCommand
            {
                MemoryId = Detail?.Id ?? EditorId,
                Category = EditorCategory,
                Scope = EditorScope,
                Text = EditorText,
                UpdatedBy = string.IsNullOrWhiteSpace(EditorUpdatedBy) ? null : EditorUpdatedBy.Trim(),
            };

            var updateResult = await _dispatcher.SendAsync(update, ct).ConfigureAwait(true);
            if (!updateResult.IsSuccess || updateResult.Value is null || !updateResult.Value.Success)
            {
                ErrorMessage = updateResult.Error ?? updateResult.Value?.Error ?? "Update failed.";
                StatusMessage = "Memory update failed.";
                return false;
            }

            Detail = updateResult.Value.Item;
            if (Detail is not null)
                PopulateEditorFromDetail();
            StatusMessage = $"Saved '{update.MemoryId}'.";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Memory save failed.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Clears the editor and begins a new memory draft.</summary>
    public void BeginNewDraft()
    {
        Detail = null;
        IsNewDraft = true;
        EditorId = "";
        EditorCategory = "";
        EditorScope = MemoryScope.Workspace;
        EditorText = "";
        EditorUpdatedBy = "";
        StatusMessage = "New memory draft.";
        ErrorMessage = null;
    }

    /// <summary>Populates editor fields from the currently loaded detail. Version stays display-only.</summary>
    public void PopulateEditorFromDetail()
    {
        if (Detail is null)
            return;

        IsNewDraft = false;
        EditorId = Detail.Id;
        EditorCategory = Detail.Category;
        EditorScope = Detail.Scope;
        EditorText = Detail.Text;
        EditorUpdatedBy = Detail.UpdatedBy ?? "";
    }
}
