using Bunit;
using Bunit.TestDoubles;
using McpServer.Cqrs;
using McpServer.UI.Core;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class DetailItemToolbarTests
{
    [Fact]
    public void TodoDetail_RendersStickyToolbarWithAdjacentTodoLinks()
    {
        var api = new TodoApiClientStub
        {
            OnListTodosAsync = (_, _) => Task.FromResult(new ListTodosResult(
            [
                new TodoListItem("TODO-003", "Third task", "Architecture", "medium", false, null),
                new TodoListItem("TODO-001", "First task", "Architecture", "high", false, null),
                new TodoListItem("TODO-002", "Second task", "Architecture", "high", false, null)
            ], 3)),
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(CreateTodoDetail("TODO-002", "Second task"))
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(parameters => parameters.Add(x => x.TodoId, "TODO-002"));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.Find("nav.detail-item-toolbar");
            Assert.Equal("/todos/TODO-001", toolbar.QuerySelector("a[rel='prev']")?.GetAttribute("href"));
            Assert.Equal("/todos/TODO-003", toolbar.QuerySelector("a[rel='next']")?.GetAttribute("href"));
            Assert.Contains("Item 2 of 3", toolbar.TextContent, StringComparison.Ordinal);
            var expanders = cut.FindAll("button.detail-card-expander");
            Assert.Single(expanders);
            Assert.Equal("true", expanders[0].GetAttribute("aria-expanded"));
        });
    }

    [Fact]
    public void SessionLogDetail_RendersItemToolbarAndPreservesTurnToolbar()
    {
        var detail = new SessionLogDetail(
            "session-002",
            "Codex",
            "Second session",
            "completed",
            "gpt-5",
            "2026-03-18T01:00:00Z",
            "2026-03-18T01:05:00Z",
            2,
            42,
            null,
            null,
            null,
            [
                new SessionLogTurnDetail("req-1", "2026-03-18T01:00:00Z", "Turn one", "Query one", "Response one", null, "completed", "gpt-5", "OpenAI", 21, null, null, null, [], [], [], [], [], [], [], [], []),
                new SessionLogTurnDetail("req-2", "2026-03-18T01:03:00Z", "Turn two", "Query two", "Response two", null, "completed", "gpt-5", "OpenAI", 21, null, null, null, [], [], [], [], [], [], [], [], [])
            ]);

        var api = new SessionLogApiClientStub
        {
            OnListSessionLogsAsync = (_, _) => Task.FromResult(new ListSessionLogsResult(
            [
                new SessionLogSummary("session-001", "Codex", "First session", "completed", "gpt-5", null, null, 1),
                new SessionLogSummary("session-002", "Codex", "Second session", "completed", "gpt-5", null, null, 2),
                new SessionLogSummary("session-003", "Codex", "Third session", "completed", "gpt-5", null, null, 1)
            ], 3, 50, 0)),
            OnGetSessionLogAsync = (_, _) => Task.FromResult<SessionLogDetail?>(detail)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ISessionLogApiClient>(api));
        ctx.Services.GetRequiredService<BunitNavigationManager>()
            .NavigateTo("/sessions/session-002?agent=Codex&model=gpt-5&text=status");
        var cut = ctx.Render<McpServer.Web.Pages.Sessions.SessionLogDetail>(parameters => parameters
            .Add(x => x.SessionId, "session-002"));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.Find("nav.detail-item-toolbar");
            Assert.Equal("/sessions/session-001?agent=Codex&model=gpt-5&text=status", toolbar.QuerySelector("a[rel='prev']")?.GetAttribute("href"));
            Assert.Equal("/sessions/session-003?agent=Codex&model=gpt-5&text=status", toolbar.QuerySelector("a[rel='next']")?.GetAttribute("href"));
            Assert.NotEmpty(cut.FindAll(".detail-field-grid"));
            var expanders = cut.FindAll("button.session-turn-expander");
            Assert.Equal(5, expanders.Count);
            Assert.All(expanders, button => Assert.Equal("true", button.GetAttribute("aria-expanded")));
            Assert.Contains("Query one", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Response one", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("No processing dialog for this turn.", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("No actions for this turn.", cut.Markup, StringComparison.Ordinal);

            var turnControls = cut.FindAll(".session-turn-controls");
            Assert.NotEmpty(turnControls);
            Assert.Contains("Turn 1 of 2", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TemplateDetail_RendersToolbarWithFilterAwareLinks()
    {
        var detail = new TemplateDetail("template-002", "Second template", "planning", ["tag-b"], "Description", "handlebars", [], "Hello");
        var api = new TemplateApiClientStub
        {
            OnListTemplatesAsync = (_, _, _, _) => Task.FromResult(new ListTemplatesResult(
            [
                new TemplateListItem("template-001", "First template", "planning", ["tag-a"], null),
                new TemplateListItem("template-002", "Second template", "planning", ["tag-b"], null),
                new TemplateListItem("template-003", "Third template", "planning", ["tag-c"], null)
            ], 3)),
            OnGetTemplateAsync = (_, _) => Task.FromResult<TemplateDetail?>(detail)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITemplateApiClient>(api));
        ctx.Services.GetRequiredService<BunitNavigationManager>()
            .NavigateTo("/templates/template-002?category=planning&tag=tag-b&keyword=hello%20world");
        var cut = ctx.Render<McpServer.Web.Pages.Templates.TemplateDetail>(parameters => parameters
            .Add(x => x.TemplateId, "template-002"));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.Find("nav.detail-item-toolbar");
            Assert.Equal("/templates/template-001?category=planning&tag=tag-b&keyword=hello%20world", toolbar.QuerySelector("a[rel='prev']")?.GetAttribute("href"));
            Assert.Equal("/templates/template-003?category=planning&tag=tag-b&keyword=hello%20world", toolbar.QuerySelector("a[rel='next']")?.GetAttribute("href"));
            Assert.NotEmpty(cut.FindAll(".detail-field-grid"));
            var expanders = cut.FindAll("button.detail-card-expander");
            Assert.Equal(3, expanders.Count);
            Assert.All(expanders, button => Assert.Equal("true", button.GetAttribute("aria-expanded")));
        });
    }

    [Fact]
    public void WorkspaceDetail_RendersToolbarWithQueryBasedLinks()
    {
        const string currentPath = @"C:\repo-two";
        var detail = new WorkspaceDetail(
            currentPath,
            "Workspace Two",
            "todo.yaml",
            null,
            null,
            false,
            true,
            null,
            null,
            "status",
            "implement",
            "plan",
            DateTimeOffset.Parse("2026-03-18T01:00:00Z"),
            DateTimeOffset.Parse("2026-03-18T01:05:00Z"),
            [],
            [],
            [],
            []);

        var api = new WorkspaceApiClientStub
        {
            OnListWorkspacesAsync = _ => Task.FromResult(new ListWorkspacesResult(
            [
                new WorkspaceSummary(@"C:\repo-one", "Workspace One", false, true),
                new WorkspaceSummary(currentPath, "Workspace Two", false, true),
                new WorkspaceSummary(@"C:\repo-three", "Workspace Three", true, true)
            ], 3)),
            OnGetWorkspaceAsync = (_, _) => Task.FromResult<WorkspaceDetail?>(detail)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<IWorkspaceApiClient>(api));
        ctx.Services.GetRequiredService<BunitNavigationManager>()
            .NavigateTo($"/workspaces/detail?path={Uri.EscapeDataString(currentPath)}");
        var cut = ctx.Render<McpServer.Web.Pages.Workspaces.WorkspaceDetail>();

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.Find("nav.detail-item-toolbar");
            Assert.Equal("/workspaces/detail?path=C%3A%5Crepo-one", toolbar.QuerySelector("a[rel='prev']")?.GetAttribute("href"));
            Assert.Equal("/workspaces/detail?path=C%3A%5Crepo-three", toolbar.QuerySelector("a[rel='next']")?.GetAttribute("href"));
            Assert.NotEmpty(cut.FindAll(".detail-field-grid"));
            var expanders = cut.FindAll("button.detail-card-expander");
            Assert.Equal(2, expanders.Count);
            Assert.All(expanders, button => Assert.Equal("true", button.GetAttribute("aria-expanded")));
        });
    }

    private static TodoDetail CreateTodoDetail(string id, string title)
        => new(
            Id: id,
            Title: title,
            Section: "Architecture",
            Priority: "high",
            Done: false,
            Estimate: "2h",
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

    private static BunitContext CreateTestContext(Action<IServiceCollection>? configureServices = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = @"E:\\repo"
            })
            .Build();

        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.Services.AddWebServices();
        ctx.Services.AddSingleton<IHealthApiClient>(new HealthApiClientStub());
        ctx.Services.AddSingleton<ITodoApiClient>(new TodoApiClientStub());
        ctx.Services.AddSingleton<ISessionLogApiClient>(new SessionLogApiClientStub());
        ctx.Services.AddSingleton<ITemplateApiClient>(new TemplateApiClientStub());
        ctx.Services.AddSingleton<IWorkspaceApiClient>(new WorkspaceApiClientStub());

        configureServices?.Invoke(ctx.Services);
        return ctx;
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

    private sealed class HealthApiClientStub : IHealthApiClient
    {
        public Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthSnapshot(DateTimeOffset.UtcNow, "Healthy", "{}"));
    }

    private sealed class SessionLogApiClientStub : ISessionLogApiClient
    {
        public Func<ListSessionLogsQuery, CancellationToken, Task<ListSessionLogsResult>>? OnListSessionLogsAsync { get; init; }
        public Func<string, CancellationToken, Task<SessionLogDetail?>>? OnGetSessionLogAsync { get; init; }

        public Task<ListSessionLogsResult> ListSessionLogsAsync(ListSessionLogsQuery query, CancellationToken cancellationToken = default)
            => OnListSessionLogsAsync?.Invoke(query, cancellationToken)
               ?? Task.FromResult(new ListSessionLogsResult([], 0, query.Limit, query.Offset));

        public Task<SessionLogDetail?> GetSessionLogAsync(string sessionId, CancellationToken cancellationToken = default)
            => OnGetSessionLogAsync?.Invoke(sessionId, cancellationToken) ?? Task.FromResult<SessionLogDetail?>(null);

        public Task<SessionLogSubmitOutcome> SubmitSessionLogAsync(SubmitSessionLogCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SessionLogDialogAppendOutcome> AppendSessionLogDialogAsync(AppendSessionLogDialogCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TemplateApiClientStub : ITemplateApiClient
    {
        public Func<string?, string?, string?, CancellationToken, Task<ListTemplatesResult>>? OnListTemplatesAsync { get; init; }
        public Func<string, CancellationToken, Task<TemplateDetail?>>? OnGetTemplateAsync { get; init; }

        public Task<ListTemplatesResult> ListTemplatesAsync(string? category, string? tag, string? keyword, CancellationToken cancellationToken = default)
            => OnListTemplatesAsync?.Invoke(category, tag, keyword, cancellationToken) ?? Task.FromResult(new ListTemplatesResult([], 0));

        public Task<TemplateDetail?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
            => OnGetTemplateAsync?.Invoke(templateId, cancellationToken) ?? Task.FromResult<TemplateDetail?>(null);

        public Task<TemplateMutationOutcome> CreateTemplateAsync(CreateTemplateCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TemplateMutationOutcome> UpdateTemplateAsync(UpdateTemplateCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TemplateMutationOutcome> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TemplateTestOutcome> TestTemplateAsync(TestTemplateQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class WorkspaceApiClientStub : IWorkspaceApiClient
    {
        public Func<CancellationToken, Task<ListWorkspacesResult>>? OnListWorkspacesAsync { get; init; }
        public Func<string, CancellationToken, Task<WorkspaceDetail?>>? OnGetWorkspaceAsync { get; init; }

        public Task<ListWorkspacesResult> ListWorkspacesAsync(CancellationToken ct = default)
            => OnListWorkspacesAsync?.Invoke(ct) ?? Task.FromResult(new ListWorkspacesResult([], 0));

        public Task<WorkspaceDetail?> GetWorkspaceAsync(string workspacePath, CancellationToken ct = default)
            => OnGetWorkspaceAsync?.Invoke(workspacePath, ct) ?? Task.FromResult<WorkspaceDetail?>(null);

        public Task<bool> UpdateWorkspacePolicyAsync(UpdateWorkspacePolicyCommand command, CancellationToken ct = default) => throw new NotImplementedException();
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
}
