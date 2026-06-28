using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

public sealed class TriageViewModelTests
{
    private const string WorkspacePath = @"F:\GitHub\TargetWorkspace";

    [Fact]
    public async Task LoadAsync_LoadsDashboardQueuesRunHistoryAndOpenTriageTodos()
    {
        var now = DateTimeOffset.UtcNow;
        var triageGroup = CreateGroup("group-collecting", "collecting", now);
        var reportGroup = CreateGroup("group-ready", "ready", now);
        var run = CreateRun("run-1", "group-ready", "completed", now);
        var openTodo = CreateOpenTodo("TODO-77", "run-1", "group-ready", now);
        var api = Substitute.For<ITriageApiClient>();
        api.GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TriageDashboardSnapshot(
                [triageGroup],
                [reportGroup],
                [run],
                TotalGroupCount: 4,
                TotalRunCount: 2)));
        api.QueryOpenCreatedTodosAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OpenTriageTodosResult(
                [openTodo],
                TotalCreatedCount: 3,
                HiddenCompletedOrMissingCount: 1,
                HydrationErrorCount: 1)));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        host.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = WorkspacePath;
        var viewModel = host.GetRequiredService<TriageViewModel>();

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(viewModel.TriageQueue);
        Assert.Single(viewModel.ReportGroupQueue);
        Assert.Single(viewModel.RunHistory);
        Assert.Single(viewModel.OpenTriageTodos);
        Assert.Equal(4, viewModel.TotalGroupCount);
        Assert.Equal(2, viewModel.TotalRunCount);
        Assert.Equal(3, viewModel.TotalCreatedTodoCount);
        Assert.Equal(1, viewModel.HiddenCompletedOrMissingTodoCount);
        Assert.Equal(1, viewModel.TodoHydrationErrorCount);
        Assert.Equal("TODO-77", viewModel.SelectedOpenTodo?.TodoId);
        Assert.Contains("completed/unavailable TODOs: 1", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("hydration errors: 1", viewModel.StatusMessage, StringComparison.Ordinal);
        await api.Received(1).GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>());
        await api.Received(1).QueryOpenCreatedTodosAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailLoaders_LoadSelectedGroupRunAndReport()
    {
        var now = DateTimeOffset.UtcNow;
        var group = CreateGroup("group-detail", "ready", now) with
        {
            Reports = [CreateReport("report-1", "group-detail", now)]
        };
        var run = CreateRun("run-detail", "group-detail", "completed", now);
        var report = CreateReport("report-detail", "group-detail", now);
        var api = Substitute.For<ITriageApiClient>();
        api.GetGroupAsync("group-detail", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TriageGroupSnapshot?>(group));
        api.GetRunAsync("run-detail", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TriageRunSnapshot?>(run));
        api.GetReportAsync("report-detail", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TriageReportSnapshot?>(report));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        var viewModel = host.GetRequiredService<TriageViewModel>();

        var groupResult = await viewModel.LoadGroupDetailAsync("group-detail");
        var runResult = await viewModel.LoadRunDetailAsync("run-detail");
        var reportResult = await viewModel.LoadReportDetailAsync("report-detail");

        Assert.Same(group, groupResult);
        Assert.Same(group, viewModel.SelectedGroupDetail);
        Assert.Same(run, runResult);
        Assert.Same(run, viewModel.SelectedRunDetail);
        Assert.Same(report, reportResult);
        Assert.Same(report, viewModel.SelectedReportDetail);
        await api.Received(1).GetGroupAsync("group-detail", Arg.Any<CancellationToken>());
        await api.Received(1).GetRunAsync("run-detail", Arg.Any<CancellationToken>());
        await api.Received(1).GetReportAsync("report-detail", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_ShowsError_WhenDashboardQueryFails()
    {
        var api = Substitute.For<ITriageApiClient>();
        api.GetDashboardAsync(null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TriageDashboardSnapshot>(new InvalidOperationException("dashboard failed")));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        var viewModel = host.GetRequiredService<TriageViewModel>();

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Equal("Triage load failed.", viewModel.StatusMessage);
        Assert.Contains("dashboard failed", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(viewModel.OpenTriageTodos);
        await api.DidNotReceive().QueryOpenCreatedTodosAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_ReportsPermissionFailure_WhenTriageReadDenied()
    {
        var api = Substitute.For<ITriageApiClient>();
        var authorization = new ConfigurableAuthorizationPolicyService()
            .SetAction(McpActionKeys.TriageRead, allowed: false, requiredRole: McpRoles.Viewer);

        using var host = UiCoreTestHost.Create(services =>
        {
            services.AddSingleton(api);
            services.AddSingleton<IAuthorizationPolicyService>(authorization);
        });
        var viewModel = host.GetRequiredService<TriageViewModel>();

        await viewModel.LoadAsync();

        Assert.Equal("Triage load failed.", viewModel.StatusMessage);
        Assert.Contains(McpRoles.Viewer, viewModel.ErrorMessage, StringComparison.Ordinal);
        await api.DidNotReceive().GetDashboardAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroupFromSelectionAsync_DispatchesEditAndRefreshesDashboard()
    {
        var now = DateTimeOffset.UtcNow;
        var createdGroup = CreateGroup("group-created", "ready", now);
        var api = Substitute.For<ITriageApiClient>();
        api.CreateGroupFromSelectionAsync(
                Arg.Is<TriageGroupSelectionSnapshot>(selection =>
                    selection.GroupIds.SequenceEqual(new[] { "group-collecting" }) &&
                    selection.ReportIds.Count == 0),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TriageGroupEditResultSnapshot(createdGroup, ["group-collecting"], 2)));
        api.GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TriageDashboardSnapshot(
                [],
                [createdGroup],
                [],
                TotalGroupCount: 1,
                TotalRunCount: 0)));
        api.QueryOpenCreatedTodosAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OpenTriageTodosResult([], 0, 0, 0)));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        host.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = WorkspacePath;
        var viewModel = host.GetRequiredService<TriageViewModel>();

        var result = await viewModel.CreateGroupFromSelectionAsync(new TriageGroupSelectionSnapshot(["group-collecting"], []));

        Assert.NotNull(result);
        Assert.Equal("group-created", result.Group.GroupId);
        Assert.Single(viewModel.ReportGroupQueue);
        Assert.Contains("Moved 2 reports into group 'group-created'.", viewModel.StatusMessage, StringComparison.Ordinal);
        await api.Received(1).CreateGroupFromSelectionAsync(Arg.Any<TriageGroupSelectionSnapshot>(), Arg.Any<CancellationToken>());
        await api.Received(1).GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    /// <summary>TEST-TRIAGE-RESUBMIT-001: failed triage groups can be resubmitted and the dashboard refreshes.</summary>
    [Fact]
    public async Task ResubmitFailedGroupAsync_DispatchesRetryAndRefreshesDashboard()
    {
        var now = DateTimeOffset.UtcNow;
        var retriedGroup = CreateGroup("group-failed", "retry_pending", now);
        var api = Substitute.For<ITriageApiClient>();
        api.RetryGroupAsync("group-failed", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(retriedGroup));
        api.GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TriageDashboardSnapshot(
                [],
                [retriedGroup],
                [],
                TotalGroupCount: 1,
                TotalRunCount: 0)));
        api.QueryOpenCreatedTodosAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OpenTriageTodosResult([], 0, 0, 0)));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        host.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = WorkspacePath;
        var viewModel = host.GetRequiredService<TriageViewModel>();

        var result = await viewModel.ResubmitFailedGroupAsync("group-failed");

        Assert.NotNull(result);
        Assert.Equal("group-failed", result.GroupId);
        Assert.Single(viewModel.ReportGroupQueue);
        Assert.Contains("Resubmitted triage group 'group-failed'.", viewModel.StatusMessage, StringComparison.Ordinal);
        await api.Received(1).RetryGroupAsync("group-failed", Arg.Any<CancellationToken>());
        await api.Received(1).GetDashboardAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    private static TriageGroupSnapshot CreateGroup(string groupId, string status, DateTimeOffset now)
        => new(
            groupId,
            status,
            ReportCount: 2,
            WorkspacePath,
            Title: $"Title for {groupId}",
            Summary: $"Summary for {groupId}",
            QuietDeadlineUtc: now.AddMinutes(5),
            CreatedTodoId: "TODO-77",
            LastError: null,
            Reports: []);

    private static TriageReportSnapshot CreateReport(string reportId, string groupId, DateTimeOffset now)
        => new(
            reportId,
            groupId,
            Status: "queued",
            Title: $"Report {reportId}",
            Summary: "Report summary",
            OriginalWorkspacePath: WorkspacePath,
            WorkspacePath,
            CreatedUtc: now.AddMinutes(-5));

    private static TriageRunSnapshot CreateRun(string runId, string groupId, string status, DateTimeOffset now)
        => new(
            runId,
            groupId,
            status,
            WorkspacePath,
            GroupStatus: "ready",
            GroupTitle: $"Group {groupId}",
            GroupSummary: "Group summary",
            ReportCount: 2,
            PromptTemplateId: "template-1",
            Prompt: "prompt",
            GroupJson: "{}",
            RawOutput: "raw",
            ResponseJson: "{}",
            Error: null,
            CreatedTodoId: "TODO-77",
            StartedUtc: now.AddMinutes(-2),
            CompletedUtc: now);

    private static OpenTriageTodoItem CreateOpenTodo(string todoId, string runId, string groupId, DateTimeOffset now)
        => new(
            todoId,
            Title: "Investigate triage finding",
            WorkspacePath,
            Section: "Triage",
            Priority: "high",
            GroupId: groupId,
            RunId: runId,
            GroupStatus: "ready",
            RunStatus: "completed",
            CreatedAtUtc: now.AddMinutes(-1),
            GroupTitle: "Group title",
            GroupSummary: "Group summary",
            ReportCount: 2,
            QuietDeadlineUtc: now.AddMinutes(5));
}
