using McpServer.Cqrs;
using McpServer.Cqrs.Mvvm;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// ViewModel for functional requirement detail and CRUD operations.
/// Extended with workspace assignment support for Director TUI.
/// </summary>
[ViewModelCommand("requirements-fr-detail", Description = "Get/create/update/delete/assign functional requirements")]
public sealed class FrDetailViewModel : AreaDetailViewModelBase<FunctionalRequirementItem>
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<FrDetailViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrDetailViewModel"/> class.
    /// </summary>
    /// <param name="dispatcher">CQRS dispatcher.</param>
    /// <param name="logger">Logger instance.</param>
    public FrDetailViewModel(
        Dispatcher dispatcher,
        ILogger<FrDetailViewModel> logger)
        : base(McpArea.Requirements)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>Loads a functional requirement by ID.</summary>
    public async Task<FunctionalRequirementItem?> LoadAsync(string id, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Loading {id}...";
        try
        {
            var result = await _dispatcher.QueryAsync(new GetFunctionalRequirementQuery(id), ct).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error ?? "Load failed.";
                StatusMessage = "Load failed.";
                return null;
            }

            Detail = result.Value;
            LastUpdatedAt = DateTimeOffset.UtcNow;
            StatusMessage = result.Value is null ? $"{id} not found." : $"Loaded {id}.";
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Load failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Creates a functional requirement.</summary>
    public async Task<FunctionalRequirementItem?> CreateAsync(string id, string title, string body, CancellationToken ct = default)
    {
        return await SaveAsync(
                new CreateFunctionalRequirementCommand(id, title, body),
                $"Creating {id}...",
                id,
                ct)
            .ConfigureAwait(true);
    }

    /// <summary>Updates a functional requirement.</summary>
    public async Task<FunctionalRequirementItem?> UpdateAsync(string id, string title, string body, CancellationToken ct = default)
    {
        return await SaveAsync(
                new UpdateFunctionalRequirementCommand(id, title, body),
                $"Saving {id}...",
                id,
                ct)
            .ConfigureAwait(true);
    }

    /// <summary>Deletes a functional requirement.</summary>
    public async Task<RequirementsMutationOutcome?> DeleteAsync(string id, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Deleting {id}...";
        try
        {
            var result = await _dispatcher.SendAsync(new DeleteFunctionalRequirementCommand(id), ct).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Error ?? "Delete failed.";
                StatusMessage = "Delete failed.";
                return null;
            }

            if (!result.Value.Success)
            {
                ErrorMessage = result.Value.Error ?? "Delete failed.";
                StatusMessage = "Delete failed.";
                return result.Value;
            }

            if (Detail is not null && string.Equals(Detail.Id, id, StringComparison.OrdinalIgnoreCase))
                Detail = null;
            StatusMessage = $"Deleted {id}.";
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Delete failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Assigns a functional requirement to a (potentially different) workspace.
    /// Used by the Director TUI "Assign WS" button.
    /// </summary>
    public async Task<RequirementsMutationOutcome?> AssignToWorkspaceAsync(string id, string workspacePath, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Assigning {id} to workspace '{workspacePath}'...";
        try
        {
            var result = await _dispatcher.SendAsync(
                new AssignFunctionalRequirementToWorkspaceCommand(id, workspacePath), 
                ct).ConfigureAwait(true);
            
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Error ?? "Assign failed.";
                StatusMessage = "Assign failed.";
                return null;
            }

            if (!result.Value.Success)
            {
                ErrorMessage = result.Value.Error ?? "Assign failed.";
                StatusMessage = "Assign failed.";
                return result.Value;
            }

            StatusMessage = $"Assigned {id} to workspace '{workspacePath}'.";
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Assign failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<FunctionalRequirementItem?> SaveAsync<TCommand>(
        TCommand command,
        string pendingStatus,
        string id,
        CancellationToken ct)
        where TCommand : ICommand<FunctionalRequirementItem>
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = pendingStatus;
        try
        {
            var result = await _dispatcher.SendAsync(command, ct).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Error ?? "Save failed.";
                StatusMessage = "Save failed.";
                return null;
            }

            Detail = result.Value;
            LastUpdatedAt = DateTimeOffset.UtcNow;
            StatusMessage = $"Saved {id}.";
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Save failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
