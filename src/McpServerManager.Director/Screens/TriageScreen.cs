using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.ViewModels;
using Terminal.Gui;

namespace McpServerManager.Director.Screens;

/// <summary>Triage dashboard screen.</summary>
internal sealed class TriageScreen : View
{
    private readonly TriageViewModel _viewModel;
    private readonly Func<OpenTriageTodoItem, Task> _openTodoAsync;
    private TableView _triageQueueTable = null!;
    private TableView _reportGroupTable = null!;
    private TableView _runHistoryTable = null!;
    private TableView _openTodosTable = null!;
    private TextView _detailView = null!;
    private TextView _statusView = null!;
    private List<TriageGroupSnapshot> _triageQueueRows = [];
    private List<TriageGroupSnapshot> _reportGroupRows = [];
    private List<TriageRunSnapshot> _runRows = [];
    private List<OpenTriageTodoItem> _todoRows = [];

    public TriageScreen(TriageViewModel viewModel, Func<OpenTriageTodoItem, Task> openTodoAsync)
    {
        _viewModel = viewModel;
        _openTodoAsync = openTodoAsync;
        Title = "Triage";
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        BuildUi();
    }

    private void BuildUi()
    {
        var topHeight = Dim.Percent(25)!;
        var secondTop = Pos.Percent(25);
        var secondHeight = Dim.Percent(25)!;
        var thirdTop = Pos.Percent(50);
        var thirdHeight = Dim.Percent(25)!;
        var fourthTop = Pos.Percent(75);

        var triageQueueFrame = CreateTableFrame("Triage Queue", 0, 0, topHeight, out _triageQueueTable);
        _triageQueueTable.SelectedCellChanged += (_, _) =>
        {
            _viewModel.SelectedTriageQueueIndex = _triageQueueTable.SelectedRow;
            _ = Task.Run(LoadSelectedTriageGroupDetailAsync);
        };
        Add(triageQueueFrame);

        var reportGroupFrame = CreateTableFrame("Report Group Queue", Pos.Percent(50), 0, topHeight, out _reportGroupTable);
        _reportGroupTable.SelectedCellChanged += (_, _) =>
        {
            _viewModel.SelectedReportGroupIndex = _reportGroupTable.SelectedRow;
            _ = Task.Run(LoadSelectedReportGroupDetailAsync);
        };
        Add(reportGroupFrame);

        var runHistoryFrame = CreateTableFrame("Run History", 0, secondTop, secondHeight, out _runHistoryTable);
        _runHistoryTable.SelectedCellChanged += (_, _) =>
        {
            _viewModel.SelectedRunIndex = _runHistoryTable.SelectedRow;
            _ = Task.Run(LoadSelectedRunDetailAsync);
        };
        Add(runHistoryFrame);

        var openTodosFrame = CreateTableFrame("Triage-Created TODOs", Pos.Percent(50), secondTop, secondHeight, out _openTodosTable);
        _openTodosTable.SelectedCellChanged += (_, _) =>
        {
            _viewModel.SelectedOpenTodoIndex = _openTodosTable.SelectedRow;
            ShowSelectedOpenTodoDetail();
        };
        Add(openTodosFrame);

        var detailFrame = new FrameView
        {
            Title = "Selection Detail",
            X = 0,
            Y = thirdTop,
            Width = Dim.Fill(),
            Height = thirdHeight,
        };
        _detailView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            Text = "",
        };
        detailFrame.Add(_detailView);
        Add(detailFrame);

        _statusView = new TextView
        {
            X = 0,
            Y = fourthTop,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = true,
            Text = "",
        };
        Add(_statusView);

        var refreshButton = new Button { X = 0, Y = Pos.AnchorEnd(1), Text = "Refresh" };
        refreshButton.Accepting += (_, _) => _ = Task.Run(LoadAsync);

        var openTodoButton = new Button { X = Pos.Right(refreshButton) + 2, Y = Pos.AnchorEnd(1), Text = "Open TODO" };
        openTodoButton.Accepting += (_, _) => _ = Task.Run(OpenSelectedTodoAsync);

        var resubmitButton = new Button { X = Pos.Right(openTodoButton) + 2, Y = Pos.AnchorEnd(1), Text = "Resubmit Run" };
        resubmitButton.Accepting += (_, _) => _ = Task.Run(ResubmitSelectedRunAsync);

        Add(refreshButton, openTodoButton, resubmitButton);
    }

    public async Task LoadAsync()
    {
        SetStatus("Loading triage...");
        await _viewModel.LoadAsync().ConfigureAwait(true);
        RebuildTables();
        SetStatus(!string.IsNullOrWhiteSpace(_viewModel.ErrorMessage)
            ? $"✗ {_viewModel.ErrorMessage}"
            : _viewModel.StatusMessage);
    }

    private async Task LoadSelectedTriageGroupDetailAsync()
    {
        var item = GetSelected(_triageQueueRows, _triageQueueTable.SelectedRow);
        if (item is null)
            return;

        var detail = await _viewModel.LoadGroupDetailAsync(item.GroupId).ConfigureAwait(true) ?? item;
        ShowGroupDetail(detail);
        SetStatus(_viewModel.StatusMessage);
    }

    private async Task LoadSelectedReportGroupDetailAsync()
    {
        var item = GetSelected(_reportGroupRows, _reportGroupTable.SelectedRow);
        if (item is null)
            return;

        var detail = await _viewModel.LoadGroupDetailAsync(item.GroupId).ConfigureAwait(true) ?? item;
        ShowGroupDetail(detail);
        SetStatus(_viewModel.StatusMessage);
    }

    private async Task LoadSelectedRunDetailAsync()
    {
        var item = GetSelected(_runRows, _runHistoryTable.SelectedRow);
        if (item is null)
            return;

        var detail = await _viewModel.LoadRunDetailAsync(item.RunId).ConfigureAwait(true) ?? item;
        ShowRunDetail(detail);
        SetStatus(_viewModel.StatusMessage);
    }

    private async Task OpenSelectedTodoAsync()
    {
        var item = GetSelected(_todoRows, _openTodosTable.SelectedRow);
        if (item is null)
        {
            SetStatus("Select a triage-created TODO first.");
            return;
        }

        if (!item.CanOpen)
        {
            SetStatus("Selected triage-created TODO details are unavailable.");
            return;
        }

        await _openTodoAsync(item).ConfigureAwait(true);
    }

    private async Task ResubmitSelectedRunAsync()
    {
        var item = GetSelected(_runRows, _runHistoryTable.SelectedRow);
        if (item is null)
        {
            SetStatus("Select a failed triage run first.");
            return;
        }

        if (!CanResubmitRun(item))
        {
            SetStatus("Selected triage run is not failed.");
            return;
        }

        var result = await _viewModel.ResubmitFailedGroupAsync(item.GroupId).ConfigureAwait(true);
        if (result is not null)
            RebuildTables();
        SetStatus(_viewModel.StatusMessage);
    }

    private void ShowSelectedOpenTodoDetail()
    {
        var item = GetSelected(_todoRows, _openTodosTable.SelectedRow);
        if (item is null)
            return;

        Application.Invoke(() => _detailView.Text = FormatOpenTodo(item));
    }

    private void RebuildTables()
    {
        _triageQueueRows = _viewModel.TriageQueue.ToList();
        _reportGroupRows = _viewModel.ReportGroupQueue.ToList();
        _runRows = _viewModel.RunHistory.ToList();
        _todoRows = _viewModel.OpenTriageTodos.ToList();

        Application.Invoke(() =>
        {
            _triageQueueTable.Table = BuildGroupTable(_triageQueueRows);
            _reportGroupTable.Table = BuildGroupTable(_reportGroupRows);
            _runHistoryTable.Table = BuildRunTable(_runRows);
            _openTodosTable.Table = BuildTodoTable(_todoRows);
            SelectFirstRow(_triageQueueTable, _triageQueueRows.Count);
            SelectFirstRow(_reportGroupTable, _reportGroupRows.Count);
            SelectFirstRow(_runHistoryTable, _runRows.Count);
            SelectFirstRow(_openTodosTable, _todoRows.Count);
        });
    }

    private static FrameView CreateTableFrame(string title, Pos x, Pos y, Dim height, out TableView table)
    {
        var frame = new FrameView
        {
            Title = title,
            X = x,
            Y = y,
            Width = Dim.Percent(50),
            Height = height,
        };

        table = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            MultiSelect = false,
        };
        table.Style.ShowHeaders = true;
        table.Style.ShowHorizontalHeaderUnderline = true;
        table.Style.ShowVerticalCellLines = true;
        table.Style.ExpandLastColumn = true;
        frame.Add(table);
        return frame;
    }

    private static DataTableSource BuildGroupTable(IReadOnlyList<TriageGroupSnapshot> rows)
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("Timestamp", typeof(string));
        table.Columns.Add("Group", typeof(string));
        table.Columns.Add("Reports", typeof(int));
        table.Columns.Add("Title", typeof(string));
        foreach (var row in rows)
            table.Rows.Add(row.Status, FormatTimestamp(row.QuietDeadlineUtc), Shorten(row.GroupId, 24), row.ReportCount, Shorten(row.Title ?? row.Summary ?? "", 46));
        return new DataTableSource(table);
    }

    private static DataTableSource BuildRunTable(IReadOnlyList<TriageRunSnapshot> rows)
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("Timestamp", typeof(string));
        table.Columns.Add("Run", typeof(string));
        table.Columns.Add("Group", typeof(string));
        table.Columns.Add("Todo", typeof(string));
        foreach (var row in rows)
            table.Rows.Add(row.Status, FormatTimestamp(row.StartedUtc), Shorten(row.RunId, 24), Shorten(row.GroupId, 24), row.CreatedTodoId ?? "");
        return new DataTableSource(table);
    }

    private static DataTableSource BuildTodoTable(IReadOnlyList<OpenTriageTodoItem> rows)
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("TODO", typeof(string));
        table.Columns.Add("Timestamp", typeof(string));
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("Workspace", typeof(string));
        foreach (var row in rows)
            table.Rows.Add(Shorten(row.TodoId, 24), FormatTimestamp(row.CreatedAtUtc), Shorten(row.Title, 42), row.GroupStatus ?? row.RunStatus ?? "", Shorten(row.WorkspacePath, 36));
        return new DataTableSource(table);
    }

    private void ShowGroupDetail(TriageGroupSnapshot group)
        => Application.Invoke(() => _detailView.Text = FormatGroup(group));

    private void ShowRunDetail(TriageRunSnapshot run)
        => Application.Invoke(() => _detailView.Text = FormatRun(run));

    private static string FormatGroup(TriageGroupSnapshot group)
    {
        var lines = new List<string>
        {
            $"Group: {group.GroupId}",
            $"Status: {group.Status}",
            $"Workspace: {group.WorkspacePath ?? ""}",
            $"Reports: {group.ReportCount}",
            $"Quiet deadline: {FormatTimestamp(group.QuietDeadlineUtc)}",
            $"Created TODO: {group.CreatedTodoId ?? ""}",
            $"Title: {group.Title ?? ""}",
            $"Summary: {group.Summary ?? ""}",
        };
        if (!string.IsNullOrWhiteSpace(group.LastError))
            lines.Add($"Error: {group.LastError}");
        if (group.Reports.Count > 0)
        {
            lines.Add("");
            lines.Add("Reports:");
            lines.AddRange(group.Reports.Select(report => $"  {report.ReportId} [{report.Status}] {report.Title ?? report.Summary ?? ""}"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRun(TriageRunSnapshot run)
    {
        var lines = new List<string>
        {
            $"Run: {run.RunId}",
            $"Status: {run.Status}",
            $"Group: {run.GroupId} [{run.GroupStatus ?? ""}]",
            $"Workspace: {run.WorkspacePath ?? ""}",
            $"Reports: {run.ReportCount}",
            $"Started: {FormatTimestamp(run.StartedUtc)}",
            $"Completed: {FormatTimestamp(run.CompletedUtc)}",
            $"Created TODO: {run.CreatedTodoId ?? ""}",
            $"Group title: {run.GroupTitle ?? ""}",
            $"Group summary: {run.GroupSummary ?? ""}",
        };
        if (!string.IsNullOrWhiteSpace(run.Error))
            lines.Add($"Error: {run.Error}");
        if (!string.IsNullOrWhiteSpace(run.ResponseJson))
        {
            lines.Add("");
            lines.Add("Result:");
            lines.Add(run.ResponseJson);
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatOpenTodo(OpenTriageTodoItem todo)
        => string.Join(Environment.NewLine, new[]
        {
            $"TODO: {todo.TodoId}",
            $"Title: {todo.Title}",
            $"Workspace: {todo.WorkspacePath}",
            $"Priority: {todo.Priority ?? ""}",
            $"Section: {todo.Section ?? ""}",
            $"Group: {todo.GroupId ?? ""} [{todo.GroupStatus ?? ""}]",
            $"Run: {todo.RunId ?? ""} [{todo.RunStatus ?? ""}]",
            $"Done: {todo.Done}",
            $"Can open: {todo.CanOpen}",
            $"Created: {FormatTimestamp(todo.CreatedAtUtc)}",
            $"Reports: {todo.ReportCount}",
            $"Group title: {todo.GroupTitle ?? ""}",
            $"Group summary: {todo.GroupSummary ?? ""}",
        });

    private void SetStatus(string text) => Application.Invoke(() => _statusView.Text = text);

    private static T? GetSelected<T>(IReadOnlyList<T> rows, int selectedRow)
        => selectedRow >= 0 && selectedRow < rows.Count ? rows[selectedRow] : default;

    private static void SelectFirstRow(TableView table, int count)
    {
        if (count > 0)
            table.SelectedRow = Math.Clamp(table.SelectedRow, 0, count - 1);
        table.SetNeedsDraw();
    }

    private static string Shorten(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 3)] + "...";

    private static bool CanResubmitRun(TriageRunSnapshot run)
        => !string.IsNullOrWhiteSpace(run.GroupId) &&
           (string.Equals(run.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(run.Status, "error", StringComparison.OrdinalIgnoreCase));

    private static string FormatTimestamp(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");

    private static string FormatTimestamp(DateTimeOffset? value)
        => value.HasValue ? FormatTimestamp(value.Value) : string.Empty;
}
