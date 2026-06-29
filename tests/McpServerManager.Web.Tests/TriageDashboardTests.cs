using Bunit;
using AngleSharp.Dom;
using System.Globalization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class TriageDashboardTests
{
    private const string WorkspacePath = @"F:\GitHub\TargetWorkspace";
    private const string SelectedWorkspacePath = @"F:\GitHub\SelectedWorkspace";

    private static readonly TodoDetail SampleTodo = new(
        Id: "TODO-77",
        Title: "Investigate triage finding",
        Section: "Triage",
        Priority: "high",
        Done: false,
        Estimate: null,
        Note: null,
        Description: [],
        TechnicalDetails: [],
        ImplementationTasks: [],
        CompletedDate: null,
        DoneSummary: null,
        Remaining: null,
        PriorityNote: null,
        Reference: null,
        DependsOn: [],
        FunctionalRequirements: [],
        TechnicalRequirements: []);

    [Fact]
    public void TriageDashboard_RendersOpenTriageTodos()
    {
        var now = new DateTimeOffset(2026, 6, 27, 18, 0, 0, TimeSpan.Zero);
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult(
                [CreateOpenTodo(now)],
                TotalCreatedCount: 1,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("TODO-77", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Triage-Created TODOs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("TODO-77", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Investigate triage finding", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("TargetWorkspace", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Timestamp", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(SortableTimestamp(now.AddMinutes(5)), cut.Markup, StringComparison.Ordinal);
        Assert.Contains(SortableTimestamp(now.AddMinutes(-2)), cut.Markup, StringComparison.Ordinal);
        Assert.Contains(SortableTimestamp(now.AddMinutes(-1)), cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TriageDashboard_UsesDataTypedFiltersForRunHistoryAndCreatedTodos()
    {
        var now = new DateTimeOffset(2026, 6, 27, 18, 0, 0, TimeSpan.Zero);
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboardWithRuns(now),
            OpenTodos = new OpenTriageTodosResult(
                CreateOpenTodosForFiltering(now),
                TotalCreatedCount: 3,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("run-target", cut.Markup, StringComparison.Ordinal));

        Assert.Equal("select", cut.Find("[aria-label='Filter run history status']").LocalName);
        Assert.Equal("datetime-local", cut.Find("input[aria-label='Filter run history timestamp from']").GetAttribute("type"));
        Assert.Equal("datetime-local", cut.Find("input[aria-label='Filter run history timestamp to']").GetAttribute("type"));
        Assert.Equal("number", cut.Find("input[aria-label='Filter run history reports min']").GetAttribute("type"));
        Assert.Equal("number", cut.Find("input[aria-label='Filter run history reports max']").GetAttribute("type"));
        Assert.Equal("select", cut.Find("[aria-label='Filter triage-created TODO group']").LocalName);
        Assert.Equal("select", cut.Find("[aria-label='Filter triage-created TODO run']").LocalName);
        Assert.Equal("datetime-local", cut.Find("input[aria-label='Filter triage-created TODO timestamp from']").GetAttribute("type"));
        Assert.Equal("datetime-local", cut.Find("input[aria-label='Filter triage-created TODO timestamp to']").GetAttribute("type"));
        Assert.Equal("number", cut.Find("input[aria-label='Filter triage-created TODO reports min']").GetAttribute("type"));
        Assert.Equal("number", cut.Find("input[aria-label='Filter triage-created TODO reports max']").GetAttribute("type"));
        Assert.Equal("select", cut.Find("[aria-label='Filter triage-created TODO workspace']").LocalName);

        cut.Find("select[aria-label='Filter run history status']").Change("failed");
        cut.Find("input[aria-label='Filter run history timestamp from']").Change(DateTimeLocal(now.AddMinutes(-10)));
        cut.Find("input[aria-label='Filter run history timestamp to']").Change(DateTimeLocal(now.AddMinutes(-10)));
        cut.Find("input[aria-label='Filter run history run']").Input("target");
        cut.Find("input[aria-label='Filter run history group']").Input("group-target");
        cut.Find("input[aria-label='Filter run history reports min']").Change("3");
        cut.Find("input[aria-label='Filter run history reports max']").Change("3");
        cut.Find("input[aria-label='Filter run history created TODO']").Input("BUG-1");

        cut.Find("input[aria-label='Filter triage-created TODO ID']").Input("TODO-B");
        cut.Find("input[aria-label='Filter triage-created TODO timestamp from']").Change(DateTimeLocal(now.AddMinutes(-3)));
        cut.Find("input[aria-label='Filter triage-created TODO timestamp to']").Change(DateTimeLocal(now.AddMinutes(-3)));
        cut.Find("input[aria-label='Filter triage-created TODO title']").Input("Beta");
        cut.Find("select[aria-label='Filter triage-created TODO group']").Change("completed");
        cut.Find("select[aria-label='Filter triage-created TODO run']").Change("triage-run-beta");
        cut.Find("input[aria-label='Filter triage-created TODO reports min']").Change("2");
        cut.Find("input[aria-label='Filter triage-created TODO reports max']").Change("2");
        cut.Find("select[aria-label='Filter triage-created TODO workspace']").Change(@"F:\GitHub\BetaWorkspace");

        Assert.Contains("run-target", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("run-alpha", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("run-zeta", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("TODO-B", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO-A", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO-C", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TriageDashboard_SortsRunHistoryAndCreatedTodosByVisibleColumns()
    {
        var now = new DateTimeOffset(2026, 6, 27, 18, 0, 0, TimeSpan.Zero);
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboardWithRuns(now),
            OpenTodos = new OpenTriageTodosResult(
                CreateOpenTodosForFiltering(now),
                TotalCreatedCount: 3,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("run-target", cut.Markup, StringComparison.Ordinal));
        Click(cut, () => FindSortButton(cut, "Run History", "Reports"));
        AssertMarkupOrder(cut.Markup, "run-alpha", "run-zeta", "run-target");

        Click(cut, () => FindSortButton(cut, "Run History", "Reports"));
        AssertMarkupOrder(cut.Markup, "run-target", "run-zeta", "run-alpha");

        Click(cut, () => FindSortButton(cut, "Triage-Created TODOs", "Reports"));
        AssertMarkupOrder(cut.Markup, "TODO-A", "TODO-B", "TODO-C");

        Click(cut, () => FindSortButton(cut, "Triage-Created TODOs", "Reports"));
        AssertMarkupOrder(cut.Markup, "TODO-C", "TODO-B", "TODO-A");
    }

    [Fact]
    public void TriageDashboard_RendersGridSortControlsWithoutRazorArtifacts()
    {
        var now = new DateTimeOffset(2026, 6, 27, 18, 0, 0, TimeSpan.Zero);
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboardWithRuns(now),
            OpenTodos = new OpenTriageTodosResult(
                CreateOpenTodosForFiltering(now),
                TotalCreatedCount: 3,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("run-target", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("RunSortSuffix", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("TodoSortSuffix", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("RunSortColumn", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("TodoSortColumn", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("descending", FindSortHeader(cut, "Run History", "Timestamp").GetAttribute("aria-sort"));
        Assert.Equal("descending", FindSortHeader(cut, "Triage-Created TODOs", "Timestamp").GetAttribute("aria-sort"));

        Click(cut, () => FindSortButton(cut, "Run History", "Reports"));
        Assert.Equal("ascending", FindSortHeader(cut, "Run History", "Reports").GetAttribute("aria-sort"));
        Click(cut, () => FindSortButton(cut, "Run History", "Reports"));
        Assert.Equal("descending", FindSortHeader(cut, "Run History", "Reports").GetAttribute("aria-sort"));

        Click(cut, () => FindSortButton(cut, "Triage-Created TODOs", "TODO"));
        Assert.Equal("ascending", FindSortHeader(cut, "Triage-Created TODOs", "TODO").GetAttribute("aria-sort"));
    }

    [Fact]
    public void TriageDashboard_RendersEmptyStates()
    {
        var triageApi = new TriageApiClientStub
        {
            Dashboard = new TriageDashboardSnapshot([], [], [], TotalGroupCount: 0, TotalRunCount: 0),
            OpenTodos = new OpenTriageTodosResult(
                [],
                TotalCreatedCount: 0,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("No triage queue items.", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("No report group queue items.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("No triage runs.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("No triage-created TODOs.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TriageDashboard_RendersLoadError()
    {
        var triageApi = new TriageApiClientStub
        {
            DashboardException = new InvalidOperationException("dashboard unavailable"),
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load triage", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("dashboard unavailable", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TriageDashboard_LoadsSelectedGroupRunAndReportDetails()
    {
        var now = new DateTimeOffset(2026, 6, 27, 18, 0, 0, TimeSpan.Zero);
        var dashboard = CreateDashboard(now);
        var reportDetail = new TriageReportSnapshot(
            "report-detail",
            "group-collecting",
            "open",
            "Report detail title",
            "Report detail summary",
            OriginalWorkspacePath: WorkspacePath,
            WorkspacePath: WorkspacePath,
            CreatedUtc: now.AddMinutes(-4));
        var groupDetail = dashboard.TriageQueue[0] with
        {
            Summary = "Loaded group detail summary",
            Reports = [reportDetail],
        };
        var runDetail = dashboard.RunHistory[0] with
        {
            GroupSummary = "Loaded run detail summary",
            ResponseJson = """{"result":"ok"}""",
        };
        var triageApi = new TriageApiClientStub
        {
            Dashboard = dashboard,
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0),
            GroupDetail = groupDetail,
            RunDetail = runDetail,
            ReportDetail = reportDetail,
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("group-collecting", cut.Markup, StringComparison.Ordinal));
        Click(cut, () => cut.Find("button[aria-label='Load triage group group-collecting details']"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["group-collecting"], triageApi.LoadedGroupIds);
            Assert.Contains("Loaded group detail summary", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("report-detail", cut.Markup, StringComparison.Ordinal);
        });

        Click(cut, () => cut.Find("button[aria-label='Load triage report report-detail details']"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["report-detail"], triageApi.LoadedReportIds);
            Assert.Contains("Report detail title", cut.Markup, StringComparison.Ordinal);
        });

        Click(cut, () => cut.Find("button[aria-label='Load triage run run-1 details']"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["run-1"], triageApi.LoadedRunIds);
            Assert.Contains("Loaded run detail summary", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("result", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TriageDashboard_LoadsUsingActiveWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult(
                [],
                TotalCreatedCount: 0,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = SelectedWorkspacePath;

        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(SelectedWorkspacePath, triageApi.DashboardWorkspacePaths);
            Assert.Contains(SelectedWorkspacePath, triageApi.OpenTodoWorkspacePaths);
        });
    }

    [Fact]
    public void TriageDashboard_OpenTodo_SetsWorkspaceAndNavigatesToTodoDetail()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult(
                [CreateOpenTodo(now)],
                TotalCreatedCount: 1,
                HiddenCompletedOrMissingCount: 0,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("TODO-77", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").First(button => button.TextContent.Trim().Equals("Open", StringComparison.Ordinal)).Click();

        var workspaceContext = ctx.Services.GetRequiredService<WorkspaceContextViewModel>();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal(WorkspacePath, workspaceContext.ActiveWorkspacePath);
        Assert.EndsWith(
            "/todos/TODO-77?workspacePath=F%3A%5CGitHub%5CTargetWorkspace&done=false",
            navigation.Uri,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TriageDashboard_CreatesNewGroupFromSelectedTriageRows()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0),
            ApplyCreateResultToDashboard = true,
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("group-collecting", cut.Markup, StringComparison.Ordinal));
        cut.Find("input[aria-label='Select triage group group-collecting']").Change(true);
        FindButton(cut, "New Group").Click();

        cut.WaitForAssertion(() =>
        {
            var selection = Assert.Single(triageApi.CreateSelections);
            Assert.Equal(["group-collecting"], selection.GroupIds);
            Assert.Empty(selection.ReportIds);
            Assert.DoesNotContain("Select triage group group-collecting", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Select report group group-created", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TriageDashboard_ConsolidatesSelectedTriageRowsIntoSelectedReportGroup()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("group-ready", cut.Markup, StringComparison.Ordinal));
        cut.Find("input[aria-label='Select triage group group-collecting']").Change(true);
        cut.Find("input[aria-label='Select report group group-ready']").Change(true);
        FindButton(cut, "Move To Group").Click();

        cut.WaitForAssertion(() =>
        {
            var call = Assert.Single(triageApi.ConsolidateSelections);
            Assert.Equal("group-ready", call.TargetGroupId);
            Assert.Equal(["group-collecting"], call.Selection.GroupIds);
        });
    }

    [Fact]
    public void TriageDashboard_MergesSelectedGroups()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() => Assert.Contains("group-ready", cut.Markup, StringComparison.Ordinal));
        cut.Find("input[aria-label='Select triage group group-collecting']").Change(true);
        cut.Find("input[aria-label='Select report group group-ready']").Change(true);
        FindButton(cut, "Combine Groups").Click();

        cut.WaitForAssertion(() =>
        {
            var call = Assert.Single(triageApi.MergeSelections);
            Assert.Equal("group-ready", call.TargetGroupId);
            Assert.Equal(["group-collecting"], call.Selection.GroupIds);
        });
    }

    /// <summary>TEST-TRIAGE-RESUBMIT-001: failed run history rows expose resubmit and retry the owning group.</summary>
    [Fact]
    public void TriageDashboard_ResubmitsFailedRunGroup()
    {
        var now = DateTimeOffset.UtcNow;
        var dashboard = CreateDashboard(now);
        var failedRun = dashboard.RunHistory[0] with
        {
            RunId = "run-failed",
            GroupId = "group-failed",
            Status = "failed",
            GroupStatus = "failed",
            CreatedTodoId = null,
            Error = "agent failed",
        };
        var triageApi = new TriageApiClientStub
        {
            Dashboard = dashboard with
            {
                RunHistory = [dashboard.RunHistory[0], failedRun],
            },
            OpenTodos = new OpenTriageTodosResult([], 0, 0, 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("run-failed", cut.Markup, StringComparison.Ordinal);
            Assert.Single(cut.FindAll("button"), button => button.TextContent.Contains("Resubmit", StringComparison.Ordinal));
        });
        FindButton(cut, "Resubmit").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["group-failed"], triageApi.RetryGroupIds);
            Assert.Contains("Resubmitted triage group 'group-failed'.", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>TEST-TRIAGE-CREATEDTODOS-001: created TODO references render even when TODO hydration is unavailable.</summary>
    [Fact]
    public void TriageDashboard_RendersCreatedTodoReference_WhenTodoCannotBeOpened()
    {
        var now = DateTimeOffset.UtcNow;
        var triageApi = new TriageApiClientStub
        {
            Dashboard = CreateDashboard(now),
            OpenTodos = new OpenTriageTodosResult(
                [
                    new OpenTriageTodoItem(
                        TodoId: "BUG-TRIAGE-002",
                        Title: "Created TODO details unavailable",
                        WorkspacePath,
                        Section: null,
                        Priority: null,
                        GroupId: "triage-group-1",
                        RunId: "triage-run-1",
                        GroupStatus: "completed",
                        RunStatus: "completed",
                        CreatedAtUtc: now.AddMinutes(-4),
                        GroupTitle: "Created TODO details unavailable",
                        GroupSummary: "TODO was created by triage but could not be hydrated.",
                        ReportCount: 1,
                        QuietDeadlineUtc: null,
                        Done: false,
                        CanOpen: false)
                ],
                TotalCreatedCount: 1,
                HiddenCompletedOrMissingCount: 1,
                HydrationErrorCount: 0)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITriageApiClient>(triageApi));
        var cut = ctx.Render<McpServerManager.Web.Pages.Triage.TriageDashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("BUG-TRIAGE-002", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Created TODO details unavailable", cut.Markup, StringComparison.Ordinal);
        });
        var openButton = Assert.Single(cut.FindAll("button"), button => button.TextContent.Trim().Equals("Open", StringComparison.Ordinal));
        Assert.True(openButton.HasAttribute("disabled"));
    }

    [Fact]
    public void TodoDetail_DirectWorkspacePathQuery_SelectsWorkspaceBeforeLoading()
    {
        ListTodosQuery? listQuery = null;
        var todoApi = new TodoApiClientStub
        {
            OnListTodosAsync = (query, _) =>
            {
                listQuery = query;
                return Task.FromResult(new ListTodosResult(
                    [new TodoListItem("TODO-77", "Investigate triage finding", "Triage", "high", Done: false, Estimate: null)],
                    TotalCount: 1));
            },
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(todoApi));
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/todos/TODO-77?workspacePath={Uri.EscapeDataString(WorkspacePath)}&done=false");
        var cut = ctx.Render<McpServerManager.Web.Pages.Todos.TodoDetail>(parameters => parameters
            .Add(page => page.TodoId, "TODO-77"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-77", cut.Markup, StringComparison.Ordinal));
        var workspaceContext = ctx.Services.GetRequiredService<WorkspaceContextViewModel>();
        Assert.Equal(WorkspacePath, workspaceContext.ActiveWorkspacePath);
        Assert.NotNull(listQuery);
        Assert.False(listQuery!.Done);
    }

    private static BunitContext CreateTestContext(Action<IServiceCollection>? configureServices = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = WorkspacePath
            })
            .Build();

        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.Services.AddWebServices();
        ctx.Services.AddSingleton<IHealthApiClient>(new HealthApiClientStub());
        ctx.Services.AddSingleton<IWorkspaceApiClient>(new WorkspaceApiClientStub());
        ctx.Services.AddSingleton<ITriageApiClient>(new TriageApiClientStub());
        ctx.Services.AddSingleton<ITodoApiClient>(new TodoApiClientStub());
        configureServices?.Invoke(ctx.Services);
        return ctx;
    }

    private static TriageDashboardSnapshot CreateDashboard(DateTimeOffset now)
        => new(
            TriageQueue:
            [
                new TriageGroupSnapshot(
                    "group-collecting",
                    "collecting",
                    ReportCount: 1,
                    WorkspacePath,
                    Title: "Collecting group",
                    Summary: "Collecting summary",
                    QuietDeadlineUtc: now.AddMinutes(5),
                    CreatedTodoId: null,
                    LastError: null,
                    Reports: [])
            ],
            ReportGroupQueue:
            [
                new TriageGroupSnapshot(
                    "group-ready",
                    "ready",
                    ReportCount: 1,
                    WorkspacePath,
                    Title: "Ready group",
                    Summary: "Ready summary",
                    QuietDeadlineUtc: now.AddMinutes(3),
                    CreatedTodoId: null,
                    LastError: null,
                    Reports: [])
            ],
            RunHistory:
            [
                new TriageRunSnapshot(
                    "run-1",
                    "group-collecting",
                    "completed",
                    WorkspacePath,
                    GroupStatus: "ready",
                    GroupTitle: "Collecting group",
                    GroupSummary: "Collecting summary",
                    ReportCount: 1,
                    PromptTemplateId: "template-1",
                    Prompt: "prompt",
                    GroupJson: "{}",
                    RawOutput: "raw",
                    ResponseJson: "{}",
                    Error: null,
                    CreatedTodoId: "TODO-77",
                    StartedUtc: now.AddMinutes(-2),
                    CompletedUtc: now)
            ],
            TotalGroupCount: 1,
            TotalRunCount: 1);

    private static TriageDashboardSnapshot CreateDashboardWithRuns(DateTimeOffset now)
    {
        var dashboard = CreateDashboard(now);
        var template = dashboard.RunHistory[0];

        return dashboard with
        {
            RunHistory =
            [
                template with
                {
                    RunId = "run-zeta",
                    GroupId = "group-zeta",
                    Status = "queued",
                    ReportCount = 2,
                    CreatedTodoId = null,
                    StartedUtc = now.AddMinutes(-5),
                },
                template with
                {
                    RunId = "run-target",
                    GroupId = "group-target",
                    Status = "failed",
                    ReportCount = 3,
                    CreatedTodoId = "BUG-1",
                    StartedUtc = now.AddMinutes(-10),
                },
                template with
                {
                    RunId = "run-alpha",
                    GroupId = "group-alpha",
                    Status = "completed",
                    ReportCount = 1,
                    CreatedTodoId = "BUG-ALPHA",
                    StartedUtc = now.AddMinutes(-1),
                },
            ],
            TotalRunCount = 3,
        };
    }

    private static OpenTriageTodoItem CreateOpenTodo(DateTimeOffset now)
        => new(
            TodoId: "TODO-77",
            Title: "Investigate triage finding",
            WorkspacePath,
            Section: "Triage",
            Priority: "high",
            GroupId: "group-collecting",
            RunId: "run-1",
            GroupStatus: "ready",
            RunStatus: "completed",
            CreatedAtUtc: now.AddMinutes(-1),
            GroupTitle: "Collecting group",
            GroupSummary: "Collecting summary",
            ReportCount: 1,
            QuietDeadlineUtc: now.AddMinutes(5));

    private static IReadOnlyList<OpenTriageTodoItem> CreateOpenTodosForFiltering(DateTimeOffset now)
        =>
        [
            CreateOpenTodo(now) with
            {
                TodoId = "TODO-C",
                Title = "Charlie triage TODO",
                WorkspacePath = @"F:\GitHub\CharlieWorkspace",
                GroupId = "triage-group-charlie",
                RunId = "triage-run-charlie",
                GroupStatus = "ready",
                RunStatus = "completed",
                CreatedAtUtc = now.AddMinutes(-1),
                ReportCount = 3,
            },
            CreateOpenTodo(now) with
            {
                TodoId = "TODO-A",
                Title = "Alpha triage TODO",
                WorkspacePath = @"F:\GitHub\AlphaWorkspace",
                GroupId = "triage-group-alpha",
                RunId = "triage-run-alpha",
                GroupStatus = "ready",
                RunStatus = "completed",
                CreatedAtUtc = now.AddMinutes(-2),
                ReportCount = 1,
            },
            CreateOpenTodo(now) with
            {
                TodoId = "TODO-B",
                Title = "Beta triage TODO",
                WorkspacePath = @"F:\GitHub\BetaWorkspace",
                GroupId = "triage-group-beta",
                RunId = "triage-run-beta",
                GroupStatus = "completed",
                RunStatus = null,
                CreatedAtUtc = now.AddMinutes(-3),
                ReportCount = 2,
            },
        ];

    private static string SortableTimestamp(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");

    private static string DateTimeLocal(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static IElement FindButton<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : IComponent
        => cut.FindAll("button").First(button => button.TextContent.Contains(text, StringComparison.Ordinal));

    private static void Click<TComponent>(IRenderedComponent<TComponent> cut, Func<IElement> findElement)
        where TComponent : IComponent
        => cut.InvokeAsync(() => findElement().Click()).GetAwaiter().GetResult();

    private static IElement FindSortButton<TComponent>(IRenderedComponent<TComponent> cut, string sectionHeading, string columnTitle)
        where TComponent : IComponent
        => FindSection(cut, sectionHeading)
            .QuerySelectorAll("thead button")
            .First(button => button.TextContent.Contains(columnTitle, StringComparison.Ordinal));

    private static IElement FindSortHeader<TComponent>(IRenderedComponent<TComponent> cut, string sectionHeading, string columnTitle)
        where TComponent : IComponent
    {
        var button = FindSortButton(cut, sectionHeading, columnTitle);
        for (var current = button.ParentElement; current is not null; current = current.ParentElement)
        {
            if (string.Equals(current.LocalName, "th", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        throw new InvalidOperationException($"Column '{columnTitle}' in section '{sectionHeading}' was not rendered inside a table header.");
    }

    private static IElement FindSection<TComponent>(IRenderedComponent<TComponent> cut, string heading)
        where TComponent : IComponent
        => cut.FindAll("section").First(section => section.TextContent.Contains(heading, StringComparison.Ordinal));

    private static void AssertMarkupOrder(string markup, params string[] values)
    {
        var previousIndex = -1;
        foreach (var value in values)
        {
            var index = markup.IndexOf(value, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Expected markup to contain '{value}'.");
            Assert.True(index > previousIndex, $"Expected '{value}' to appear after the previous value.");
            previousIndex = index;
        }
    }

    private sealed class TriageApiClientStub : ITriageApiClient
    {
        public TriageDashboardSnapshot Dashboard { get; set; } = new([], [], [], 0, 0);
        public OpenTriageTodosResult OpenTodos { get; init; } = new([], 0, 0, 0);
        public Exception? DashboardException { get; init; }
        public Exception? OpenTodosException { get; init; }
        public TriageGroupSnapshot? GroupDetail { get; init; }
        public TriageRunSnapshot? RunDetail { get; init; }
        public TriageReportSnapshot? ReportDetail { get; init; }
        public bool ApplyCreateResultToDashboard { get; init; }
        public List<string?> DashboardWorkspacePaths { get; } = [];
        public List<string?> OpenTodoWorkspacePaths { get; } = [];
        public List<string> LoadedGroupIds { get; } = [];
        public List<string> LoadedRunIds { get; } = [];
        public List<string> LoadedReportIds { get; } = [];
        public List<TriageGroupSelectionSnapshot> CreateSelections { get; } = [];
        public List<(string TargetGroupId, TriageGroupSelectionSnapshot Selection)> ConsolidateSelections { get; } = [];
        public List<(string TargetGroupId, TriageGroupSelectionSnapshot Selection)> MergeSelections { get; } = [];
        public List<string> RetryGroupIds { get; } = [];

        public Task<TriageDashboardSnapshot> GetDashboardAsync(string? workspacePath, CancellationToken cancellationToken = default)
        {
            DashboardWorkspacePaths.Add(workspacePath);
            if (DashboardException is not null)
                throw DashboardException;

            return Task.FromResult(Dashboard);
        }

        public Task<TriageGroupQuerySnapshot> QueryGroupsAsync(string? status, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TriageGroupQuerySnapshot([], 0));

        public Task<TriageGroupSnapshot?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            LoadedGroupIds.Add(groupId);
            return Task.FromResult(GroupDetail?.GroupId == groupId
                ? GroupDetail
                : Dashboard.TriageQueue.Concat(Dashboard.ReportGroupQueue).FirstOrDefault(group => string.Equals(group.GroupId, groupId, StringComparison.Ordinal)));
        }

        public Task<TriageReportSnapshot?> GetReportAsync(string reportId, CancellationToken cancellationToken = default)
        {
            LoadedReportIds.Add(reportId);
            return Task.FromResult(ReportDetail?.ReportId == reportId
                ? ReportDetail
                : Dashboard.TriageQueue.Concat(Dashboard.ReportGroupQueue)
                    .SelectMany(group => group.Reports)
                    .FirstOrDefault(report => string.Equals(report.ReportId, reportId, StringComparison.Ordinal)));
        }

        public Task<TriageRunQuerySnapshot> QueryRunsAsync(string? status, string? groupId, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TriageRunQuerySnapshot([], 0));

        public Task<TriageRunSnapshot?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
        {
            LoadedRunIds.Add(runId);
            return Task.FromResult(RunDetail?.RunId == runId
                ? RunDetail
                : Dashboard.RunHistory.FirstOrDefault(run => string.Equals(run.RunId, runId, StringComparison.Ordinal)));
        }

        public Task<OpenTriageTodosResult> QueryOpenCreatedTodosAsync(string? workspacePath, CancellationToken cancellationToken = default)
        {
            OpenTodoWorkspacePaths.Add(workspacePath);
            if (OpenTodosException is not null)
                throw OpenTodosException;

            return Task.FromResult(OpenTodos);
        }

        public Task<TriageGroupEditResultSnapshot> CreateGroupFromSelectionAsync(TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        {
            CreateSelections.Add(selection);
            var result = CreateEditResult("group-created");
            if (ApplyCreateResultToDashboard)
            {
                Dashboard = Dashboard with
                {
                    TriageQueue = Dashboard.TriageQueue
                        .Where(group => !selection.GroupIds.Contains(group.GroupId, StringComparer.Ordinal))
                        .ToList(),
                    ReportGroupQueue = Dashboard.ReportGroupQueue.Concat([result.Group]).ToList(),
                };
            }

            return Task.FromResult(result);
        }

        public Task<TriageGroupEditResultSnapshot> ConsolidateIntoGroupAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        {
            ConsolidateSelections.Add((targetGroupId, selection));
            return Task.FromResult(CreateEditResult(targetGroupId));
        }

        public Task<TriageGroupEditResultSnapshot> MergeGroupsAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        {
            MergeSelections.Add((targetGroupId, selection));
            return Task.FromResult(CreateEditResult(targetGroupId));
        }

        public Task<TriageGroupSnapshot> RetryGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            RetryGroupIds.Add(groupId);
            return Task.FromResult(new TriageGroupSnapshot(
                groupId,
                "retry_pending",
                ReportCount: 1,
                WorkspacePath,
                Title: "Retried group",
                Summary: "Retried summary",
                QuietDeadlineUtc: DateTimeOffset.UtcNow,
                CreatedTodoId: null,
                LastError: null,
                Reports: []));
        }

        private static TriageGroupEditResultSnapshot CreateEditResult(string groupId)
            => new(
                new TriageGroupSnapshot(
                    groupId,
                    "queued",
                    ReportCount: 2,
                    WorkspacePath,
                    Title: "Edited group",
                    Summary: "Edited summary",
                    QuietDeadlineUtc: DateTimeOffset.UtcNow,
                    CreatedTodoId: null,
                    LastError: null,
                    Reports: []),
                [],
                MovedReportCount: 1);
    }

    private sealed class TodoApiClientStub : ITodoApiClient
    {
        public Func<ListTodosQuery, CancellationToken, Task<ListTodosResult>>? OnListTodosAsync { get; init; }
        public Func<string, CancellationToken, Task<TodoDetail?>>? OnGetTodoAsync { get; init; }

        public Task<ListTodosResult> ListTodosAsync(ListTodosQuery query, CancellationToken cancellationToken = default)
            => OnListTodosAsync?.Invoke(query, cancellationToken) ?? Task.FromResult(new ListTodosResult([], 0));

        public Task<TodoDetail?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default)
            => OnGetTodoAsync?.Invoke(todoId, cancellationToken) ?? Task.FromResult<TodoDetail?>(null);

        public Task<TodoMutationOutcome> CreateTodoAsync(CreateTodoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoMutationOutcome> UpdateTodoAsync(UpdateTodoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoMutationOutcome> DeleteTodoAsync(DeleteTodoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoRequirementsAnalysis> AnalyzeTodoRequirementsAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoPromptOutput> GenerateTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoPromptOutput> GenerateTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoPromptOutput> GenerateTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<string> StreamTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<string> StreamTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<string> StreamTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class WorkspaceApiClientStub : IWorkspaceApiClient
    {
        public Task<ListWorkspacesResult> ListWorkspacesAsync(CancellationToken ct = default)
            => Task.FromResult(new ListWorkspacesResult(
                [
                    new WorkspaceSummary(@"F:\GitHub\AlphaWorkspace", "Alpha Workspace", IsPrimary: false, IsEnabled: true),
                    new WorkspaceSummary(@"F:\GitHub\BetaWorkspace", "Beta Workspace", IsPrimary: false, IsEnabled: true),
                    new WorkspaceSummary(@"F:\GitHub\CharlieWorkspace", "Charlie Workspace", IsPrimary: false, IsEnabled: true),
                    new WorkspaceSummary(WorkspacePath, "Target Workspace", IsPrimary: true, IsEnabled: true),
                    new WorkspaceSummary(SelectedWorkspacePath, "Selected Workspace", IsPrimary: false, IsEnabled: true),
                ],
                TotalCount: 5));

        public Task<WorkspaceDetail?> GetWorkspaceAsync(string workspacePath, CancellationToken ct = default) => Task.FromResult<WorkspaceDetail?>(null);
        public Task<bool> UpdateWorkspacePolicyAsync(UpdateWorkspacePolicyCommand command, CancellationToken ct = default) => Task.FromResult(false);
        public Task<WorkspaceMutationOutcome> CreateWorkspaceAsync(CreateWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceMutationOutcome> UpdateWorkspaceAsync(UpdateWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceMutationOutcome> DeleteWorkspaceAsync(DeleteWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> GetWorkspaceStatusAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> StartWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> StopWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceHealthState> CheckWorkspaceHealthAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceGlobalPromptState> GetWorkspaceGlobalPromptAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceGlobalPromptState> UpdateWorkspaceGlobalPromptAsync(UpdateWorkspaceGlobalPromptCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceInitInfo> InitWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class HealthApiClientStub : IHealthApiClient
    {
        public Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthSnapshot(DateTimeOffset.UtcNow, "healthy", """{"status":"healthy"}"""));
    }
}
