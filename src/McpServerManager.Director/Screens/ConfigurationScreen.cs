using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Terminal.Gui;

namespace McpServerManager.Director.Screens;

/// <summary>
/// Terminal.Gui screen for viewing and editing server configuration key/value pairs.
/// </summary>
internal sealed class ConfigurationScreen : View
{
    private readonly ConfigurationViewModel _viewModel;
    private readonly ILogger<ConfigurationScreen> _logger;
    private bool _syncingSelectedValueFromViewModel;

    private ListView _keyListView = null!;
    private TextField _valueField = null!;
    private Label _keyLabel = null!;
    private TextView _statusView = null!;
    private Button _saveButton = null!;
    private Button _refreshButton = null!;
    private Button _newKeyButton = null!;

    public ConfigurationScreen(
        ConfigurationViewModel viewModel,
        ILogger<ConfigurationScreen>? logger = null)
    {
        _viewModel = viewModel;
        _logger = logger ?? NullLogger<ConfigurationScreen>.Instance;

        Title = "Configuration";
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        BuildUi();

        _viewModel.PropertyChanged += (_, e) =>
        {
            Application.Invoke(() => ApplyViewModelChanges(e.PropertyName));
        };
    }

    private void BuildUi()
    {
        // Left panel: key list
        var keysLabel = new Label
        {
            X = 0,
            Y = 0,
            Text = "Keys:",
        };
        Add(keysLabel);

        _keyListView = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(40),
            Height = Dim.Fill(4),
            CanFocus = true,
        };
        _keyListView.SelectedItemChanged += OnKeySelected;
        Add(_keyListView);

        // Right panel: value editor
        _keyLabel = new Label
        {
            X = Pos.Right(_keyListView) + 1,
            Y = 0,
            Width = Dim.Fill(),
            Text = "Selected key: (none)",
        };
        Add(_keyLabel);

        var valueLabel = new Label
        {
            X = Pos.Right(_keyListView) + 1,
            Y = 1,
            Text = "Value:",
        };
        Add(valueLabel);

        _valueField = new TextField
        {
            X = Pos.Right(_keyListView) + 1,
            Y = 2,
            Width = Dim.Fill(12),
            Height = 1,
            CanFocus = true,
        };
        _valueField.TextChanged += OnValueFieldTextChanged;
        Add(_valueField);

        _saveButton = new Button
        {
            X = Pos.Right(_valueField) + 1,
            Y = 2,
            Text = "Save",
        };
        _saveButton.Accepting += (_, _) => _ = Task.Run(SaveAsync);
        Add(_saveButton);

        // Status bar at bottom
        _statusView = new TextView
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 2,
            ReadOnly = true,
            WordWrap = true,
            Text = "Ready.",
        };
        Add(_statusView);

        _refreshButton = new Button
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Text = "Refresh",
        };
        _refreshButton.Accepting += (_, _) => _ = Task.Run(LoadAsync);
        Add(_refreshButton);

        _newKeyButton = new Button
        {
            X = Pos.Right(_refreshButton) + 2,
            Y = Pos.AnchorEnd(1),
            Text = "New Key",
        };
        _newKeyButton.Accepting += (_, _) => ShowNewKeyDialog();
        Add(_newKeyButton);
    }

    public async Task LoadAsync()
    {
        Application.Invoke(() =>
        {
            _statusView.Text = "Loading...";
            _refreshButton.Enabled = false;
        });

        try
        {
            await _viewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            Application.Invoke(() => _statusView.Text = $"Error: {ex.Message}");
        }
        finally
        {
            Application.Invoke(() => _refreshButton.Enabled = true);
        }
    }

    private async Task SaveAsync()
    {
        Application.Invoke(() =>
        {
            _saveButton.Enabled = false;
        });

        try
        {
            await _viewModel.SaveAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            Application.Invoke(() => _statusView.Text = $"Save error: {ex.Message}");
        }
        finally
        {
            Application.Invoke(() => _saveButton.Enabled = true);
        }
    }

    private void OnKeySelected(object? sender, ListViewItemEventArgs e)
    {
        var keys = _viewModel.Keys;
        if (e.Item >= 0 && e.Item < keys.Count)
            _viewModel.SelectKey(keys[e.Item]);
    }

    private void OnValueFieldTextChanged(object? sender, EventArgs e)
    {
        if (_syncingSelectedValueFromViewModel)
            return;

        var nextValue = _valueField.Text?.ToString();
        if (string.Equals(_viewModel.SelectedValue, nextValue, StringComparison.Ordinal))
            return;

        _viewModel.SelectedValue = nextValue;
    }

    private void ShowNewKeyDialog()
    {
        var dialog = new Dialog
        {
            Title = "New Configuration Key",
            Width = 62,
            Height = 11,
        };

        var keyLabel = new Label { X = 1, Y = 1, Text = "Key:  " };
        var keyField = new TextField
        {
            X = 8,
            Y = 1,
            Width = Dim.Fill(2),
            Text = string.Empty,
        };

        var valueLabel = new Label { X = 1, Y = 3, Text = "Value:" };
        var valueField = new TextField
        {
            X = 8,
            Y = 3,
            Width = Dim.Fill(2),
            Text = string.Empty,
        };

        var saveBtn = new Button { Text = "Save", Enabled = false };
        var cancelBtn = new Button { Text = "Cancel" };

        void UpdateSaveEnabled()
        {
            saveBtn.Enabled =
                !string.IsNullOrWhiteSpace(keyField.Text?.ToString()) &&
                !string.IsNullOrWhiteSpace(valueField.Text?.ToString());
        }

        keyField.TextChanged += (_, _) => UpdateSaveEnabled();
        valueField.TextChanged += (_, _) => UpdateSaveEnabled();

        saveBtn.Accepting += (_, _) =>
        {
            var key = (keyField.Text?.ToString() ?? string.Empty).Trim();
            var value = (valueField.Text?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            Application.RequestStop();
            _ = Task.Run(() => DoCreateKeyAsync(key, value));
        };

        cancelBtn.Accepting += (_, _) => Application.RequestStop();

        dialog.Add(keyLabel, keyField, valueLabel, valueField);
        dialog.AddButton(saveBtn);
        dialog.AddButton(cancelBtn);
        Application.Run(dialog);
    }

    private async Task DoCreateKeyAsync(string key, string value)
    {
        Application.Invoke(() =>
        {
            _newKeyButton.Enabled = false;
            _refreshButton.Enabled = false;
        });

        try
        {
            await _viewModel.CreateKeyAsync(key, value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            Application.Invoke(() => _statusView.Text = $"Create error: {ex.Message}");
        }
        finally
        {
            Application.Invoke(() =>
            {
                _newKeyButton.Enabled = true;
                _refreshButton.Enabled = true;
            });
        }
    }

    private void ApplyViewModelChanges(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(ConfigurationViewModel.Keys):
                _keyListView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(_viewModel.Keys));
                if (!string.IsNullOrWhiteSpace(_viewModel.SelectedKey))
                {
                    var selectedIndex = _viewModel.Keys.ToList().IndexOf(_viewModel.SelectedKey);
                    if (selectedIndex >= 0)
                        _keyListView.SelectedItem = selectedIndex;
                }
                break;

            case nameof(ConfigurationViewModel.SelectedKey):
                var key = _viewModel.SelectedKey;
                _keyLabel.Text = key is null ? "Selected key: (none)" : $"Selected key: {key}";
                // Sync list selection if key was changed programmatically (e.g. after reload)
                if (key is not null)
                {
                    var idx = _viewModel.Keys.ToList().IndexOf(key);
                    if (idx >= 0 && _keyListView.SelectedItem != idx)
                        _keyListView.SelectedItem = idx;
                }
                break;

            case nameof(ConfigurationViewModel.SelectedValue):
                var nextValue = _viewModel.SelectedValue ?? string.Empty;
                if (string.Equals(_valueField.Text?.ToString(), nextValue, StringComparison.Ordinal))
                    break;

                _syncingSelectedValueFromViewModel = true;
                try
                {
                    _valueField.Text = nextValue;
                }
                finally
                {
                    _syncingSelectedValueFromViewModel = false;
                }
                break;

            case nameof(ConfigurationViewModel.StatusMessage):
            case nameof(ConfigurationViewModel.ErrorMessage):
                var status = _viewModel.ErrorMessage ?? _viewModel.StatusMessage ?? string.Empty;
                _statusView.Text = status;
                break;

            case nameof(ConfigurationViewModel.IsLoading):
                _refreshButton.Enabled = !_viewModel.IsLoading;
                break;

            case nameof(ConfigurationViewModel.IsSaving):
                _saveButton.Enabled = !_viewModel.IsSaving;
                _newKeyButton.Enabled = !_viewModel.IsSaving;
                break;
        }
    }
}
