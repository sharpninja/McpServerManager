using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels;
using Terminal.Gui;

namespace McpServerManager.Director.Screens;

/// <summary>
/// Terminal.Gui screen that binds to <see cref="WorkspaceListViewModel"/>.
/// Displays workspaces in a TableView and an editable detail form.
/// </summary>
internal sealed class WorkspaceListScreen : View
{
    private readonly WorkspaceListViewModel _listVm;
    private readonly WorkspaceDetailViewModel _detailVm;
    private readonly WorkspacePolicyViewModel _policyVm;
    private readonly DirectorMcpContext? _directorContext;
    private readonly ViewModelBinder _binder = new();
    private readonly List<WorkspaceSummary> _rows = [];
    private readonly SemaphoreSlim _detailLoadGate = new(1, 1);
    private int _detailLoadRequestVersion;
    private string? _lastAutoDetailWorkspacePath;
    private TableView _tableView = null!;
    private TextField _pathField = null!;
    private TextField _nameField = null!;
    private TextField _todoPathField = null!;
    private TextField _dataDirectoryField = null!;
    private TextField _tunnelProviderField = null!;
    private TextField _runAsField = null!;
    private CheckBox _primaryCheckBox = null!;
    private CheckBox _enabledCheckBox = null!;
    private TextView _promptTemplateEditor = null!;
    private TextView _statusPromptEditor = null!;
    private TextView _implementPromptEditor = null!;
    private TextView _planPromptEditor = null!;
    private TextView _summaryView = null!;
    private TextView _statusLabel = null!;
    private Label _detailTitleLabel = null!;

    public WorkspaceListScreen(
        WorkspaceListViewModel listVm,
        WorkspaceDetailViewModel detailVm,
        WorkspacePolicyViewModel policyVm,
        DirectorMcpContext? directorContext = null)
    {
        _listVm = listVm;
        _detailVm = detailVm;
        _policyVm = policyVm;
        _directorContext = directorContext;
        Title = "Workspaces";
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        BuildUi();
        BeginNewWorkspaceDraft(setStatus: false);
    }

    private void BuildUi()
    {
        var statusLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "Loading...",
        };
        Add(statusLabel);

        var errorColorScheme = Colors.ColorSchemes.TryGetValue("Error", out var errScheme) ? errScheme : null;
        var errorField = new TextField
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Text = "",
            ReadOnly = true,
            Visible = false,
        };
        if (errorColorScheme is not null)
            errorField.ColorScheme = errorColorScheme;
        Add(errorField);

        _tableView = new TableView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Percent(28),
            FullRowSelect = true,
            MultiSelect = false,
        };
        _tableView.SelectedCellChanged += (_, _) => QueueSelectedRowDetailRefresh();
        Add(_tableView);

        var editorY = Pos.Bottom(_tableView);
        _detailTitleLabel = new Label
        {
            X = 0,
            Y = editorY,
            Width = Dim.Fill(),
            Text = "Detail: (select a workspace row)",
        };
        Add(_detailTitleLabel);

        var row1Y = Pos.Bottom(_detailTitleLabel);
        Add(new Label { X = 0, Y = row1Y, Text = "Path:" });
        _pathField = new TextField { X = 7, Y = row1Y, Width = Dim.Fill(), Text = "", ReadOnly = true };
        Add(_pathField);

        var row2Y = Pos.Bottom(_pathField);
        Add(new Label { X = 0, Y = row2Y, Text = "Name:" });
        _nameField = new TextField { X = 7, Y = row2Y, Width = 36, Text = "" };
        Add(_nameField);

        Add(new Label { X = 45, Y = row2Y, Text = "Todo:" });
        _todoPathField = new TextField { X = 52, Y = row2Y, Width = Dim.Fill(), Text = "" };
        Add(_todoPathField);

        var row3Y = Pos.Bottom(_nameField);
        Add(new Label { X = 0, Y = row3Y, Text = "Data:" });
        _dataDirectoryField = new TextField { X = 7, Y = row3Y, Width = 36, Text = "" };
        Add(_dataDirectoryField);

        Add(new Label { X = 45, Y = row3Y, Text = "Tunnel:" });
        _tunnelProviderField = new TextField { X = 53, Y = row3Y, Width = 18, Text = "" };
        Add(_tunnelProviderField);

        Add(new Label { X = 73, Y = row3Y, Text = "RunAs:" });
        _runAsField = new TextField { X = 80, Y = row3Y, Width = Dim.Fill(), Text = "" };
        Add(_runAsField);

        var row4Y = Pos.Bottom(_dataDirectoryField);
        _primaryCheckBox = new CheckBox { X = 0, Y = row4Y, Text = "Primary" };
        _enabledCheckBox = new CheckBox { X = 13, Y = row4Y, Text = "Enabled" };
        Add(_primaryCheckBox, _enabledCheckBox);

        ApplyEditableScheme(_pathField, _nameField, _todoPathField, _dataDirectoryField,
            _tunnelProviderField, _runAsField);

        var promptsY = Pos.Bottom(_primaryCheckBox);
        var promptFrame = CreateEditorFrame("Prompt Template", 0, promptsY, Dim.Percent(50)!, 4, out _promptTemplateEditor);
        Add(promptFrame);

        var statusFrame = CreateEditorFrame("Status Prompt", Pos.Right(promptFrame), promptsY, Dim.Fill()!, 4, out _statusPromptEditor);
        Add(statusFrame);

        var implementFrame = CreateEditorFrame("Implement Prompt", 0, Pos.Bottom(promptFrame), Dim.Percent(50)!, 4, out _implementPromptEditor);
        Add(implementFrame);

        var planFrame = CreateEditorFrame("Plan Prompt", Pos.Right(implementFrame), Pos.Bottom(statusFrame), Dim.Fill()!, 4, out _planPromptEditor);
        Add(planFrame);

        _summaryView = new TextView
        {
            X = 0,
            Y = Pos.Bottom(implementFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            ReadOnly = true,
            WordWrap = true,
            Text = "",
        };
        Add(_summaryView);

        _statusLabel = new TextView
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 1,
            ReadOnly = true,
            WordWrap = true,
            Text = "",
        };
        Add(_statusLabel);

        var refreshBtn = new Button { X = 0, Y = Pos.AnchorEnd(1), Text = "Refresh" };
        refreshBtn.Accepting += (_, _) => _ = Task.Run(LoadAsync);
        Add(refreshBtn);

        var detailBtn = new Button { X = Pos.Right(refreshBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Reload Detail" };
        detailBtn.Accepting += (_, _) => _ = Task.Run(() => LoadSelectedDetailAsync(forceStatusMessage: true));
        Add(detailBtn);

        var newBtn = new Button { X = Pos.Right(detailBtn) + 2, Y = Pos.AnchorEnd(1), Text = "New" };
        newBtn.Accepting += (_, _) => BeginNewWorkspaceDraft();
        Add(newBtn);

        var saveBtn = new Button { X = Pos.Right(newBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Save" };
        saveBtn.Accepting += (_, _) => _ = Task.Run(SaveSelectedWorkspaceAsync);
        Add(saveBtn);

        var deleteBtn = new Button { X = Pos.Right(saveBtn) + 2, Y = Pos.AnchorEnd(1), Text = "Delete" };
        deleteBtn.Accepting += (_, _) => _ = Task.Run(() => DeleteSelectedWorkspaceAsync());
        Add(deleteBtn);

        var countLabel = new Label
        {
            X = Pos.Right(deleteBtn) + 2,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Text = "",
        };
        Add(countLabel);

        _binder.BindProperty(_listVm, nameof(_listVm.IsLoading), () =>
        {
            statusLabel.Text = _listVm.IsLoading ? "Loading workspaces..." : "Workspaces";
            refreshBtn.Enabled = !_listVm.IsLoading;
            detailBtn.Enabled = !_listVm.IsLoading;
        });

        _binder.BindProperty(_listVm, nameof(_listVm.ErrorMessage), () =>
        {
            errorField.Visible = !string.IsNullOrEmpty(_listVm.ErrorMessage);
            errorField.Text = _listVm.ErrorMessage ?? "";
        });

        _binder.BindProperty(_listVm, nameof(_listVm.TotalCount), () =>
        {
            countLabel.Text = $"Total: {_listVm.TotalCount}";
        });

        _binder.BindCollection(_listVm.Workspaces, _tableView, items =>
        {
            _rows.Clear();
            _rows.AddRange(items);
            return new EnumerableTableSource<WorkspaceSummary>(
                items,
                new Dictionary<string, Func<WorkspaceSummary, object>>
                {
                    ["Name"] = ws => ws.Name,
                    ["Path"] = ws => ws.WorkspacePath,
                    ["Primary"] = ws => ws.IsPrimary ? "Y" : "",
                    ["Enabled"] = ws => ws.IsEnabled ? "Y" : "N",
                });
        });
    }

    /// <summary>Triggers initial data load.</summary>
    public async Task LoadAsync()
    {
        SetStatus("Loading workspaces...");
        await _listVm.LoadAsync().ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(_listVm.ErrorMessage))
        {
            ClearDetail($"Detail: {_listVm.ErrorMessage}");
            SetStatus(_listVm.ErrorMessage);
            return;
        }

        if (_rows.Count == 0)
        {
            ClearDetail("Detail: (no workspaces)");
            BeginNewWorkspaceDraft(setStatus: false);
            SetStatus("No workspaces.");
            return;
        }

        _lastAutoDetailWorkspacePath = null;
        await LoadSelectedDetailAsync(fallbackToFirst: true).ConfigureAwait(true);
    }

    internal async Task LoadSelectedDetailAsync(bool fallbackToFirst = true, bool forceStatusMessage = false)
    {
        var row = _tableView.SelectedRow;
        if (row < 0 || row >= _rows.Count)
        {
            if (!fallbackToFirst || _rows.Count == 0)
            {
                if (forceStatusMessage)
                    SetStatus("Select a workspace row first.");
                return;
            }

            row = 0;
        }

        await LoadWorkspaceDetailAsync(_rows[row]).ConfigureAwait(true);
    }

    internal async Task SaveSelectedWorkspaceAsync()
    {
        SyncEditorFieldsToViewModel();
        var targetPath = _detailVm.EditorWorkspacePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            SetStatus("Path is required.");
            return;
        }

        await _detailVm.SaveAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(_detailVm.ErrorMessage))
        {
            SetStatus(_detailVm.ErrorMessage);
            return;
        }

        if (_detailVm.Detail is not null)
        {
            _policyVm.LoadFromDetail(_detailVm.Detail);
            SyncEditorFieldsFromViewModel();
            ShowDetailText($"Detail: {_detailVm.Detail.Name}", FormatDetail(_detailVm.Detail));
            targetPath = _detailVm.Detail.WorkspacePath;
        }

        SetStatus(_detailVm.MutationMessage ?? "Workspace saved.");
        await _listVm.LoadAsync().ConfigureAwait(true);

        var updated = _listVm.Workspaces.FirstOrDefault(ws =>
            string.Equals(ws.WorkspacePath, targetPath, StringComparison.OrdinalIgnoreCase));
        if (updated is not null)
            await LoadWorkspaceDetailAsync(updated).ConfigureAwait(true);
    }

    internal async Task DeleteSelectedWorkspaceAsync(bool requireConfirmation = true)
    {
        SyncEditorFieldsToViewModel();
        var workspacePath = _detailVm.WorkspacePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            SetStatus("Path is required to delete.");
            return;
        }

        if (requireConfirmation)
        {
            var confirm = 1;
            InvokeOnUiThread(() =>
                confirm = MessageBox.Query("Confirm Delete", $"Delete workspace '{workspacePath}'?", "Delete", "Cancel"));
            if (confirm != 0)
                return;
        }

        await _detailVm.DeleteAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(_detailVm.ErrorMessage))
        {
            SetStatus(_detailVm.ErrorMessage);
            return;
        }

        _policyVm.ClearPolicy();
        BeginNewWorkspaceDraft(setStatus: false);
        SetStatus(_detailVm.MutationMessage ?? "Workspace deleted.");
        await _listVm.LoadAsync().ConfigureAwait(true);
    }

    internal void BeginNewWorkspaceDraft(bool setStatus = true)
    {
        _detailVm.BeginNewDraft();
        _policyVm.ClearPolicy();
        SyncEditorFieldsFromViewModel();
        ClearDetail("Detail: (new workspace draft)");
        if (setStatus)
            SetStatus("Editor set to new workspace draft.");
    }

    internal void SyncEditorFieldsFromViewModel()
    {
        var snapshot = WorkspaceEditorSnapshot.FromViewModel(_detailVm);
        InvokeOnUiThread(() =>
        {
            _pathField.ReadOnly = !_detailVm.IsNewDraft;
            _pathField.Text = snapshot.WorkspacePath;
            _nameField.Text = snapshot.Name;
            _todoPathField.Text = snapshot.TodoPath;
            _dataDirectoryField.Text = snapshot.DataDirectory;
            _tunnelProviderField.Text = snapshot.TunnelProvider;
            _runAsField.Text = snapshot.RunAs;
            _primaryCheckBox.CheckedState = snapshot.IsPrimary ? CheckState.Checked : CheckState.UnChecked;
            _enabledCheckBox.CheckedState = snapshot.IsEnabled ? CheckState.Checked : CheckState.UnChecked;
            _promptTemplateEditor.Text = snapshot.PromptTemplate;
            _statusPromptEditor.Text = snapshot.StatusPrompt;
            _implementPromptEditor.Text = snapshot.ImplementPrompt;
            _planPromptEditor.Text = snapshot.PlanPrompt;
        });
    }

    internal void SyncEditorFieldsToViewModel()
    {
        var snapshot = new WorkspaceEditorSnapshot(
            _pathField.Text?.ToString() ?? "",
            _nameField.Text?.ToString() ?? "",
            _todoPathField.Text?.ToString() ?? "",
            _dataDirectoryField.Text?.ToString() ?? "",
            _tunnelProviderField.Text?.ToString() ?? "",
            _runAsField.Text?.ToString() ?? "",
            _primaryCheckBox.CheckedState == CheckState.Checked,
            _enabledCheckBox.CheckedState == CheckState.Checked,
            _promptTemplateEditor.Text?.ToString() ?? "",
            _statusPromptEditor.Text?.ToString() ?? "",
            _implementPromptEditor.Text?.ToString() ?? "",
            _planPromptEditor.Text?.ToString() ?? "");

        snapshot.ApplyTo(_detailVm);
    }

    private async Task LoadWorkspaceDetailAsync(WorkspaceSummary selected)
    {
        var requestVersion = Interlocked.Increment(ref _detailLoadRequestVersion);
        await _detailLoadGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (requestVersion != Volatile.Read(ref _detailLoadRequestVersion))
                return;

            _detailVm.WorkspacePath = selected.WorkspacePath;
            await _detailVm.LoadAsync().ConfigureAwait(true);

            if (requestVersion != Volatile.Read(ref _detailLoadRequestVersion))
                return;

            if (!string.IsNullOrWhiteSpace(_detailVm.ErrorMessage))
            {
                ClearDetail($"Detail: {selected.Name}");
                SetStatus(_detailVm.ErrorMessage!);
                return;
            }

            if (_detailVm.Detail is null)
            {
                ClearDetail($"Detail: {selected.Name}");
                SetStatus("Workspace not found.");
                return;
            }

            _policyVm.LoadFromDetail(_detailVm.Detail);
            SyncEditorFieldsFromViewModel();
            ShowDetailText($"Detail: {_detailVm.Detail.Name}", FormatDetail(_detailVm.Detail));
            SetStatus($"Loaded workspace '{_detailVm.Detail.Name}'. Policy tab is synced to this workspace.");
        }
        finally
        {
            _detailLoadGate.Release();
        }
    }

    private void QueueSelectedRowDetailRefresh()
    {
        var row = _tableView.SelectedRow;
        if (row < 0 || row >= _rows.Count)
            return;

        var workspacePath = _rows[row].WorkspacePath;
        if (string.Equals(_lastAutoDetailWorkspacePath, workspacePath, StringComparison.OrdinalIgnoreCase))
            return;

        _lastAutoDetailWorkspacePath = workspacePath;
        _ = Task.Run(() => LoadSelectedDetailAsync(fallbackToFirst: false));
    }

    private void ClearDetail(string title) => ShowDetailText(title, "");

    private void ShowDetailText(string title, string text)
    {
        InvokeOnUiThread(() =>
        {
            _detailTitleLabel.Text = title;
            _summaryView.Text = text;
        });
    }

    private void SetStatus(string text) => InvokeOnUiThread(() => _statusLabel.Text = text);

    private static FrameView CreateEditorFrame(
        string title,
        Pos x,
        Pos y,
        Dim width,
        Dim height,
        out TextView editor)
    {
        var frame = new FrameView
        {
            Title = title,
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };

        editor = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WordWrap = true,
            Text = "",
        };
        ApplyEditableScheme(editor);
        frame.Add(editor);
        return frame;
    }

    private static string FormatDetail(WorkspaceDetail detail)
    {
        var lines = new List<string>
        {
            $"Path: {detail.WorkspacePath}",
            $"Name: {detail.Name}",
            $"Todo Path: {detail.TodoPath}",
            $"Data Dir: {detail.DataDirectory ?? ""}",
            $"Tunnel Provider: {detail.TunnelProvider ?? ""}",
            $"Run As: {detail.RunAs ?? ""}",
            $"Primary: {detail.IsPrimary}",
            $"Enabled: {detail.IsEnabled}",
            $"Created: {detail.DateTimeCreated:yyyy-MM-dd HH:mm:ss zzz}",
            $"Modified: {detail.DateTimeModified:yyyy-MM-dd HH:mm:ss zzz}",
        };

        AppendList(lines, "Banned Licenses", detail.BannedLicenses);
        AppendList(lines, "Banned Countries", detail.BannedCountriesOfOrigin);
        AppendList(lines, "Banned Organizations", detail.BannedOrganizations);
        AppendList(lines, "Banned Individuals", detail.BannedIndividuals);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendList(List<string> lines, string title, IReadOnlyList<string> values)
    {
        lines.Add("");
        lines.Add($"{title}:");
        if (values.Count == 0)
        {
            lines.Add("  (none)");
            return;
        }

        lines.AddRange(values.Select(v => $"  - {v}"));
    }

    private static void ApplyEditableScheme(params View[] views)
    {
        if (!Colors.ColorSchemes.TryGetValue("Editable", out var scheme))
            return;

        foreach (var v in views)
            v.ColorScheme = scheme;
    }

    private static void InvokeOnUiThread(Action action)
    {
        try
        {
            if (Application.Driver is null)
            {
                action();
                return;
            }

            Application.Invoke(action);
        }
        catch (InvalidOperationException)
        {
            action();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) _binder.Dispose();
        base.Dispose(disposing);
    }
}
