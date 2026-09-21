using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels;
using Terminal.Gui;

namespace McpServerManager.Director.Screens;

/// <summary>
/// Terminal.Gui screen for workspace memory management.
/// Displays a filterable grid with detail preview and add/update/remove.
/// </summary>
internal sealed class MemoryScreen : View
{
    private readonly MemoryListViewModel _listVm;
    private readonly MemoryDetailViewModel _detailVm;
    private readonly ViewModelBinder _binder = new();
    private TableView _tableView = null!;
    private Label _statusLabel = null!;
    private TextView _detailView = null!;
    private Button _refreshBtn = null!;
    private TextField _categoryFilter = null!;
    private TextField _keywordFilter = null!;
    private TextField _scopeFilter = null!;

    /// <summary>Initializes a new instance of the <see cref="MemoryScreen"/> class.</summary>
    public MemoryScreen(MemoryListViewModel listVm, MemoryDetailViewModel detailVm)
    {
        _listVm = listVm;
        _detailVm = detailVm;
        Title = "Memory";
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        BuildUi();
    }

    private void BuildUi()
    {
        var scopeLabel = new Label { X = 0, Y = 0, Text = "Scope:" };
        _scopeFilter = new TextField
        {
            X = Pos.Right(scopeLabel) + 1,
            Y = 0,
            Width = 12,
            Text = "",
        };

        var catLabel = new Label { X = Pos.Right(_scopeFilter) + 2, Y = 0, Text = "Category:" };
        _categoryFilter = new TextField
        {
            X = Pos.Right(catLabel) + 1,
            Y = 0,
            Width = 15,
            Text = "",
        };

        var kwLabel = new Label { X = Pos.Right(_categoryFilter) + 2, Y = 0, Text = "Search:" };
        _keywordFilter = new TextField
        {
            X = Pos.Right(kwLabel) + 1,
            Y = 0,
            Width = 20,
            Text = "",
        };

        var filterBtn = new Button { X = Pos.Right(_keywordFilter) + 1, Y = 0, Text = "Filter" };
        filterBtn.Accepting += (_, _) =>
        {
            _listVm.Scope = ParseScope(_scopeFilter.Text?.ToString());
            _listVm.Category = string.IsNullOrWhiteSpace(_categoryFilter.Text?.ToString()) ? null : _categoryFilter.Text.ToString();
            _listVm.Keyword = string.IsNullOrWhiteSpace(_keywordFilter.Text?.ToString()) ? null : _keywordFilter.Text.ToString();
            _ = Task.Run(LoadAsync);
        };

        Add(scopeLabel, _scopeFilter, catLabel, _categoryFilter, kwLabel, _keywordFilter, filterBtn);

        _statusLabel = new Label
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Text = "Workspace Memory",
        };
        Add(_statusLabel);

        var errorField = new TextField
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Text = "",
            ReadOnly = true,
            Visible = false,
        };
        var errorColorScheme = Colors.ColorSchemes.TryGetValue("Error", out var errScheme) ? errScheme : null;
        if (errorColorScheme is not null)
            errorField.ColorScheme = errorColorScheme;
        Add(errorField);

        _tableView = new TableView
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
            FullRowSelect = true,
            MultiSelect = false,
        };
        _tableView.SelectedCellChanged += (_, e) =>
        {
            _listVm.SelectedIndex = e.NewRow;
        };
        Add(_tableView);

        var detailLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_tableView),
            Text = "Text Preview:",
        };
        Add(detailLabel);

        _detailView = new TextView
        {
            X = 0,
            Y = Pos.Bottom(detailLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            ReadOnly = true,
        };
        Add(_detailView);

        _refreshBtn = new Button { X = 0, Y = Pos.AnchorEnd(1), Text = "Refresh" };
        _refreshBtn.Accepting += (_, _) => _ = Task.Run(LoadAsync);

        var createBtn = new Button { X = Pos.Right(_refreshBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Add" };
        createBtn.Accepting += (_, _) => ShowMemoryDialog(isNew: true);

        var editBtn = new Button { X = Pos.Right(createBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Edit" };
        editBtn.Accepting += (_, _) => ShowMemoryDialog(isNew: false);

        var deleteBtn = new Button { X = Pos.Right(editBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Remove" };
        deleteBtn.Accepting += (_, _) => _ = Task.Run(DeleteSelectedAsync);

        var countLabel = new Label
        {
            X = Pos.Right(deleteBtn) + 2,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Text = "",
        };

        Add(_refreshBtn, createBtn, editBtn, deleteBtn, countLabel);

        _binder.BindProperty(_listVm, nameof(_listVm.IsLoading), () =>
        {
            _statusLabel.Text = _listVm.IsLoading ? "⏳ Loading memories..." : "Workspace Memory";
            _refreshBtn.Enabled = !_listVm.IsLoading;
        });

        _binder.BindProperty(_listVm, nameof(_listVm.ErrorMessage), () =>
        {
            errorField.Visible = !string.IsNullOrEmpty(_listVm.ErrorMessage);
            errorField.Text = _listVm.ErrorMessage ?? "";
        });

        _binder.BindProperty(_listVm, nameof(_listVm.TotalCount), () =>
        {
            countLabel.Text = $"Memories: {_listVm.TotalCount}";
        });

        _binder.BindProperty(_listVm, nameof(_listVm.SelectedItem), () =>
        {
            var selected = _listVm.SelectedItem;
            if (selected is null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _detailVm.LoadAsync(selected.Id).ConfigureAwait(true);
                    var detail = _detailVm.Detail;
                    Application.Invoke(() =>
                    {
                        _detailView.Text = FormatPreview(detail);
                        _detailView.SetNeedsDraw();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    Application.Invoke(() =>
                    {
                        _detailView.Text = $"Error: {ex.Message}";
                        _detailView.SetNeedsDraw();
                    });
                }
            });
        });

        _binder.BindCollection(_listVm.Items, _tableView, items =>
        {
            return new EnumerableTableSource<MemoryListItem>(
                items,
                new Dictionary<string, Func<MemoryListItem, object>>
                {
                    ["ID"] = t => Truncate(t.Id, 24),
                    ["Category"] = t => Truncate(t.Category, 16),
                    ["Scope"] = t => t.Scope.ToString(),
                    ["Version"] = t => t.Version.ToString(),
                    ["Updated"] = t => t.UpdatedAtUtc.ToString("u"),
                    ["Text"] = t => Truncate(t.Text, 40),
                });
        });
    }

    /// <summary>Triggers initial data load.</summary>
    public async Task LoadAsync()
    {
        await _listVm.LoadAsync().ConfigureAwait(true);
    }

    private MemoryListItem? GetSelectedMemory() => _listVm.SelectedItem;

    private void ShowMemoryDialog(bool isNew)
    {
        if (!isNew)
        {
            var selected = GetSelectedMemory();
            if (selected is null)
                return;

            if (_detailVm.Detail is null || _detailVm.Detail.Id != selected.Id)
            {
                _ = Task.Run(async () =>
                {
                    await _detailVm.LoadAsync(selected.Id).ConfigureAwait(true);
                    _detailVm.PopulateEditorFromDetail();
                    Application.Invoke(() => OpenEditorDialog(isNew: false));
                });
                return;
            }

            _detailVm.PopulateEditorFromDetail();
        }
        else
        {
            _detailVm.BeginNewDraft();
        }

        OpenEditorDialog(isNew);
    }

    private void OpenEditorDialog(bool isNew)
    {
        var dlg = new Dialog
        {
            Title = isNew ? "Add Memory" : $"Edit Memory: {_detailVm.EditorId}",
            Width = Math.Min(100, Math.Max(70, Application.Top?.Frame.Width - 10 ?? 90)),
            Height = Math.Min(22, Math.Max(14, Application.Top?.Frame.Height - 6 ?? 18)),
        };

        var idLabel = new Label { X = 1, Y = 1, Text = "ID:" };
        var idField = new TextField { X = 16, Y = 1, Width = Dim.Fill(2), Text = _detailVm.EditorId, ReadOnly = !isNew };

        var catLabel = new Label { X = 1, Y = 2, Text = "Category:" };
        var catField = new TextField { X = 16, Y = 2, Width = Dim.Fill(2), Text = _detailVm.EditorCategory };

        var scopeLabel = new Label { X = 1, Y = 3, Text = "Scope:" };
        var scopeField = new TextField { X = 16, Y = 3, Width = Dim.Fill(2), Text = _detailVm.EditorScope.ToString() };
        CheckBox? globalMemoryToggle = null;
        if (_detailVm.ShowGlobalMemoryToggle)
        {
            globalMemoryToggle = new CheckBox
            {
                X = 16,
                Y = 3,
                Width = Dim.Fill(2),
                Text = "Global Memory",
                CheckedState = _detailVm.GlobalMemory ? CheckState.Checked : CheckState.UnChecked,
            };
        }

        var updatedByLabel = new Label { X = 1, Y = 4, Text = "Updated By:" };
        var updatedByField = new TextField { X = 16, Y = 4, Width = Dim.Fill(2), Text = _detailVm.EditorUpdatedBy };

        var versionLabel = new Label
        {
            X = 1,
            Y = 5,
            Text = isNew ? "Version: (new)" : $"Version: {_detailVm.DisplayVersion?.ToString() ?? "-"} (display only)",
        };

        var textLabel = new Label { X = 1, Y = 7, Text = "Text:" };
        var textField = new TextView
        {
            X = 1,
            Y = 8,
            Width = Dim.Fill(2),
            Height = Dim.Fill(3),
            Text = _detailVm.EditorText,
            WordWrap = true,
        };

        dlg.Add(idLabel, idField, catLabel, catField, scopeLabel,
            updatedByLabel, updatedByField, versionLabel, textLabel, textField);
        if (globalMemoryToggle is not null)
            dlg.Add(globalMemoryToggle);
        else
            dlg.Add(scopeField);

        var saveBtn = new Button { Text = "Save" };
        saveBtn.Accepting += (_, _) =>
        {
            _detailVm.EditorId = idField.Text?.ToString() ?? "";
            _detailVm.EditorCategory = catField.Text?.ToString() ?? "";
            if (globalMemoryToggle is not null)
                _detailVm.GlobalMemory = globalMemoryToggle.CheckedState == CheckState.Checked;
            else
                _detailVm.EditorScope = ParseScope(scopeField.Text?.ToString()) ?? MemoryScope.Workspace;
            _detailVm.EditorUpdatedBy = updatedByField.Text?.ToString() ?? "";
            _detailVm.EditorText = textField.Text?.ToString() ?? "";

            if (!_detailVm.CanSaveMemory)
            {
                MessageBox.ErrorQuery(
                    "Save Failed",
                    MemoryScopePolicy.WorkspaceScopeRequiresActiveWorkspace,
                    "OK");
                return;
            }

            _ = Task.Run(async () =>
            {
                var ok = await _detailVm.SaveAsync().ConfigureAwait(true);
                if (ok)
                {
                    Application.Invoke(() => Application.RequestStop());
                    await LoadAsync().ConfigureAwait(true);
                }
                else
                {
                    var err = _detailVm.ErrorMessage ?? "Memory save failed.";
                    Application.Invoke(() => MessageBox.ErrorQuery("Save Failed", err, "OK"));
                }
            });
        };

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Accepting += (_, _) => Application.RequestStop();

        dlg.AddButton(saveBtn);
        dlg.AddButton(cancelBtn);
        Application.Run(dlg);
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = GetSelectedMemory();
        if (selected is null)
            return;

        try
        {
            if (_detailVm.Detail?.Id != selected.Id)
                await _detailVm.LoadAsync(selected.Id).ConfigureAwait(true);

            var ok = await _detailVm.DeleteAsync().ConfigureAwait(true);
            if (ok)
            {
                await LoadAsync().ConfigureAwait(true);
            }
            else
            {
                var err = _detailVm.ErrorMessage ?? "Remove failed.";
                Application.Invoke(() => MessageBox.ErrorQuery("Remove Failed", err, "OK"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
            Application.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "OK"));
        }
    }

    private static MemoryScope? ParseScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), "Effective", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<MemoryScope>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatPreview(MemoryDetail? detail)
    {
        if (detail is null)
            return "(No content)";

        return $"Id: {detail.Id}{Environment.NewLine}" +
               $"Category: {detail.Category}{Environment.NewLine}" +
               $"Scope: {detail.Scope}{Environment.NewLine}" +
               $"Version: {detail.Version} (display only){Environment.NewLine}" +
               $"Updated: {detail.UpdatedAtUtc:u} by {detail.UpdatedBy ?? "-"}{Environment.NewLine}{Environment.NewLine}" +
               detail.Text;
    }

    private static string Truncate(string value, int maxLen)
    {
        if (value.Length <= maxLen)
            return value.PadRight(maxLen);
        return string.Concat(value.AsSpan(0, maxLen - 3), "...");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _binder.Dispose();
        base.Dispose(disposing);
    }
}
