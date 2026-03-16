using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServer.UI.Core.Messages;
using Microsoft.Extensions.Logging;

namespace McpServer.UI.Core.ViewModels;

/// <summary>ViewModel for the server configuration management screen.</summary>
public sealed partial class ConfigurationViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<ConfigurationViewModel> _logger;

    /// <summary>Initializes a new configuration ViewModel.</summary>
    public ConfigurationViewModel(Dispatcher dispatcher, ILogger<ConfigurationViewModel> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [ObservableProperty]
    private IReadOnlyList<string> _keys = [];

    [ObservableProperty]
    private string? _selectedKey;

    [ObservableProperty]
    private string? _selectedValue;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    private IReadOnlyDictionary<string, string> _allValues = new Dictionary<string, string>();

    /// <summary>Loads all configuration keys from the server.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var previouslySelectedKey = SelectedKey;
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading configuration...";

        try
        {
            var result = await _dispatcher
                .QueryAsync<IReadOnlyDictionary<string, string>>(
                    new GetConfigurationValuesQuery(), ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                _allValues = new Dictionary<string, string>();
                Keys = [];
                ErrorMessage = result.Error ?? "Unknown error loading configuration.";
                StatusMessage = "Configuration load failed.";
                return;
            }

            _allValues = result.Value ?? new Dictionary<string, string>();
            Keys = [.. _allValues.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)];
            StatusMessage = $"Loaded {Keys.Count} keys.";

            // Re-apply selected key after reload - preserve value from server
            if (previouslySelectedKey is not null && _allValues.TryGetValue(previouslySelectedKey, out var refreshedValue))
            {
                SelectedKey = previouslySelectedKey;
                SelectedValue = refreshedValue;
            }
            else if (previouslySelectedKey is not null)
            {
                SelectedKey = null;
                SelectedValue = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            _allValues = new Dictionary<string, string>();
            Keys = [];
            ErrorMessage = ex.Message;
            StatusMessage = "Configuration load failed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Selects a key and populates the value text box.</summary>
    public void SelectKey(string? key)
    {
        SelectedKey = key;
        SelectedValue = key is not null && _allValues.TryGetValue(key, out var v) ? v : null;
        ErrorMessage = null;
        StatusMessage = key is not null ? $"Selected: {key}" : null;
    }

    /// <summary>
    /// Creates a new configuration key/value pair via PATCH, then reloads and selects the new key.
    /// </summary>
    public async Task CreateKeyAsync(string key, string value, CancellationToken ct = default)
    {
        var trimmedKey = key?.Trim() ?? string.Empty;
        var trimmedValue = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedKey))
        {
            StatusMessage = "Key cannot be empty.";
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            StatusMessage = "Value cannot be empty.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = $"Creating {trimmedKey}...";

        try
        {
            var patch = new Dictionary<string, string?> { [trimmedKey] = trimmedValue };
            var result = await _dispatcher
                .SendAsync<IReadOnlyDictionary<string, string>>(
                    new PatchConfigurationValuesCommand(patch), ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error ?? "Unknown error creating configuration key.";
                StatusMessage = "Create failed.";
                return;
            }

            StatusMessage = "Created. Reloading...";

            await LoadAsync(ct).ConfigureAwait(true);

            // Select the new key if it exists after reload
            if (_allValues.ContainsKey(trimmedKey))
                SelectKey(trimmedKey);

            StatusMessage = $"Created and reloaded: {trimmedKey}";
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Create failed.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Saves the current selected key/value pair via PATCH, then reloads to confirm
    /// the persisted value.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedKey))
        {
            StatusMessage = "No key selected. Select a key from the list before saving.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = $"Saving {SelectedKey}...";

        try
        {
            var patch = new Dictionary<string, string?> { [SelectedKey] = SelectedValue };
            var result = await _dispatcher
                .SendAsync<IReadOnlyDictionary<string, string>>(
                    new PatchConfigurationValuesCommand(patch), ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error ?? "Unknown error saving configuration.";
                StatusMessage = "Save failed.";
                return;
            }

            StatusMessage = $"Saved. Reloading...";

            // Reload to confirm persisted value; preserve selected key
            var savedKey = SelectedKey;
            await LoadAsync(ct).ConfigureAwait(true);

            // Restore selection if key still present after reload
            if (savedKey is not null && _allValues.ContainsKey(savedKey))
                SelectKey(savedKey);

            StatusMessage = $"Saved and reloaded: {savedKey}";
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Save failed.";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
