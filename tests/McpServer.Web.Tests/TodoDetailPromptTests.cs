using Bunit;
using McpServer.Cqrs;
using McpServer.UI.Core;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServer.Web.Tests;

/// <summary>
/// Bunit tests covering the Copilot Status / Plan / Implementation prompt buttons and inline output panel
/// added to the TodoDetail page, as well as the fix that keeps the detail visible after a prompt error.
/// </summary>
public sealed class TodoDetailPromptTests
{
    // ---------------------------------------------------------------------------
    // Helper todo used across tests
    // ---------------------------------------------------------------------------

    private static readonly TodoDetail SampleTodo = new(
        Id: "TODO-001",
        Title: "Sample Task",
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

    // ---------------------------------------------------------------------------
    // Button rendering
    // ---------------------------------------------------------------------------

    [Fact]
    public void TodoDetailPage_RendersThreeCopilotButtons_WhenTodoLoaded()
    {
        var api = new TodoApiClientStub { OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo) };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Copilot Status", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Copilot Plan", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Copilot Implementation", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_RendersDoneButton_WhenTodoIsNotDone()
    {
        var api = new TodoApiClientStub { OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo) };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("> Done", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_ClickingDoneButton_MarksTodoDone()
    {
        UpdateTodoCommand? updateCommand = null;
        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnUpdateTodoAsync = (command, _) =>
            {
                updateCommand = command;
                return Task.FromResult(new TodoMutationOutcome(
                    Success: true,
                    Error: null,
                    Item: SampleTodo with
                    {
                        Done = true,
                        CompletedDate = command.CompletedDate
                    }));
            }
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").First(b => b.TextContent.Contains("Done", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("TODO marked done.", cut.Markup, StringComparison.Ordinal));
        Assert.NotNull(updateCommand);
        Assert.True(updateCommand!.Done);
        Assert.False(string.IsNullOrWhiteSpace(updateCommand.CompletedDate));
        Assert.DoesNotContain("> Done", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_DoesNotRenderPromptButtons_WhenNoTodoSelected()
    {
        var api = new TodoApiClientStub();

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, string.Empty));

        // Should show blank-slate, not the buttons
        cut.WaitForAssertion(() => Assert.Contains("No todo selected", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Copilot Status", cut.Markup, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Prompt output panel
    // ---------------------------------------------------------------------------

    [Fact]
    public void TodoDetailPage_ShowsPromptOutputPanel_AfterStatusPromptSucceeds()
    {
        var expectedOutput = new TodoPromptOutput(
            TodoId: "TODO-001",
            PromptType: "status",
            Lines: ["Line 1", "Line 2"],
            Text: "Line 1\nLine 2");

        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGenerateStatusPromptAsync = (_, _) => Task.FromResult(expectedOutput)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        // Wait for detail to load
        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        // Click the "Copilot Status" button
        var statusButton = cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Status", StringComparison.Ordinal));
        statusButton.Click();

        // Prompt output panel should appear with content
        cut.WaitForAssertion(() => Assert.Contains("prompt-output-panel", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Line 1\nLine 2", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("status", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_ShowsPromptOutputPanel_AfterPlanPromptSucceeds()
    {
        var expectedOutput = new TodoPromptOutput(
            TodoId: "TODO-001",
            PromptType: "plan",
            Lines: ["Plan line"],
            Text: "Plan line");

        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGeneratePlanPromptAsync = (_, _) => Task.FromResult(expectedOutput)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        var planButton = cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Plan", StringComparison.Ordinal));
        planButton.Click();

        cut.WaitForAssertion(() => Assert.Contains("prompt-output-panel", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("plan", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Plan line", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_ShowsPromptOutputPanel_AfterImplementPromptSucceeds()
    {
        var expectedOutput = new TodoPromptOutput(
            TodoId: "TODO-001",
            PromptType: "implement",
            Lines: ["Implement line"],
            Text: "Implement line");

        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGenerateImplementPromptAsync = (_, _) => Task.FromResult(expectedOutput)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        var implButton = cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Implementation", StringComparison.Ordinal));
        implButton.Click();

        cut.WaitForAssertion(() => Assert.Contains("prompt-output-panel", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("implement", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Implement line", cut.Markup, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Error handling: detail must remain visible after a prompt failure
    // ---------------------------------------------------------------------------

    [Fact]
    public void TodoDetailPage_ShowsPromptErrorInline_WhenStatusPromptFails_AndDetailStillVisible()
    {
        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGenerateStatusPromptAsync = (_, _) => Task.FromException<TodoPromptOutput>(
                new InvalidOperationException("status prompt network error"))
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        var statusButton = cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Status", StringComparison.Ordinal));
        statusButton.Click();

        // The inline prompt-error flash should appear
        cut.WaitForAssertion(() => Assert.Contains("Prompt generation failed", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("status prompt network error", cut.Markup, StringComparison.Ordinal);

        // The todo heading must still be present (detail panel not collapsed)
        Assert.Contains("TODO-001 — Sample Task", cut.Markup, StringComparison.Ordinal);

        // No prompt output panel should appear on error
        Assert.DoesNotContain("prompt-output-panel", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailPage_DoesNotShowLoadErrorStyle_WhenOnlyPromptFails()
    {
        // After a successful load, a prompt error must NOT show the "Failed to load todo" banner
        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGenerateStatusPromptAsync = (_, _) => Task.FromException<TodoPromptOutput>(
                new InvalidOperationException("prompt failure"))
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        var statusButton = cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Status", StringComparison.Ordinal));
        statusButton.Click();

        cut.WaitForAssertion(() => Assert.Contains("Prompt generation failed", cut.Markup, StringComparison.Ordinal));

        // The "Failed to load todo" message (load-error branch) must NOT appear
        Assert.DoesNotContain("Failed to load todo", cut.Markup, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Clear button removes prompt output panel
    // ---------------------------------------------------------------------------

    [Fact]
    public void TodoDetailPage_ClearButton_RemovesPromptOutputPanel()
    {
        var expectedOutput = new TodoPromptOutput("TODO-001", "status", ["hello"], "hello");

        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (_, _) => Task.FromResult<TodoDetail?>(SampleTodo),
            OnGenerateStatusPromptAsync = (_, _) => Task.FromResult(expectedOutput)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var cut = ctx.Render<McpServer.Web.Pages.Todos.TodoDetail>(p => p.Add(x => x.TodoId, "TODO-001"));

        cut.WaitForAssertion(() => Assert.Contains("TODO-001", cut.Markup, StringComparison.Ordinal));

        // Generate prompt
        cut.FindAll("button").First(b => b.TextContent.Contains("Copilot Status", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.Contains("prompt-output-panel", cut.Markup, StringComparison.Ordinal));

        // Click clear
        var clearButton = cut.FindAll("button").First(b => b.TextContent.Contains("Clear", StringComparison.Ordinal));
        clearButton.Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("prompt-output-panel", cut.Markup, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------
    // Stale prompt output cleared on todo navigation (ViewModel-level test)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TodoDetailViewModel_ClearsPromptOutputAndError_WhenLoadAsyncCalledForNewTodo()
    {
        // This tests the ViewModel behaviour that backs the "navigating to a new todo clears
        // stale prompt output" behaviour without needing full Bunit navigation.
        var promptOutput = new TodoPromptOutput("TODO-001", "status", ["output"], "output");

        var api = new TodoApiClientStub
        {
            OnGetTodoAsync = (id, _) => id == "TODO-001"
                ? Task.FromResult<TodoDetail?>(SampleTodo)
                : Task.FromResult<TodoDetail?>(SampleTodo with { Id = "TODO-002", Title = "Second Task" }),
            OnGenerateStatusPromptAsync = (_, _) => Task.FromResult(promptOutput)
        };

        using var ctx = CreateTestContext(services => services.AddSingleton<ITodoApiClient>(api));
        var vm = ctx.Services.GetRequiredService<TodoDetailViewModel>();

        // Load TODO-001 and generate a status prompt
        vm.TodoId = "TODO-001";
        await vm.LoadAsync();
        await vm.GenerateStatusPromptAsync();

        Assert.NotNull(vm.PromptOutput);
        Assert.Null(vm.PromptErrorMessage);

        // Simulate navigating to TODO-002: same thing OnParametersSetAsync does
        vm.TodoId = "TODO-002";
        await vm.LoadAsync();

        // Prompt output from the previous todo must have been cleared
        Assert.Null(vm.PromptOutput);
        Assert.Null(vm.PromptErrorMessage);
        Assert.Equal("TODO-002", vm.Detail?.Id);
    }

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------

    private static Bunit.BunitContext CreateTestContext(Action<IServiceCollection>? configureServices = null)
    {
        var ctx = new Bunit.BunitContext();

        // TodoDetail.razor embeds MonacoEditor (StandaloneCodeEditor) which makes JS interop
        // calls during OnAfterRenderAsync. Set Loose mode so those calls are silently ignored
        // rather than throwing UnhandledJSInteropCallException.
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

        // Default no-op todo API (tests override individual methods via OnXxx)
        ctx.Services.AddSingleton<ITodoApiClient>(new TodoApiClientStub());

        configureServices?.Invoke(ctx.Services);
        return ctx;
    }

    // ---------------------------------------------------------------------------
    // Stubs
    // ---------------------------------------------------------------------------

    private sealed class TodoApiClientStub : ITodoApiClient
    {
        public Func<ListTodosQuery, CancellationToken, Task<ListTodosResult>>? OnListTodosAsync { get; init; }
        public Func<string, CancellationToken, Task<TodoDetail?>>? OnGetTodoAsync { get; init; }
        public Func<UpdateTodoCommand, CancellationToken, Task<TodoMutationOutcome>>? OnUpdateTodoAsync { get; init; }
        public Func<string, CancellationToken, Task<TodoPromptOutput>>? OnGenerateStatusPromptAsync { get; init; }
        public Func<string, CancellationToken, Task<TodoPromptOutput>>? OnGeneratePlanPromptAsync { get; init; }
        public Func<string, CancellationToken, Task<TodoPromptOutput>>? OnGenerateImplementPromptAsync { get; init; }

        public Task<ListTodosResult> ListTodosAsync(ListTodosQuery query, CancellationToken cancellationToken = default)
            => OnListTodosAsync?.Invoke(query, cancellationToken) ?? Task.FromResult(new ListTodosResult([], 0));

        public Task<TodoDetail?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default)
            => OnGetTodoAsync?.Invoke(todoId, cancellationToken) ?? Task.FromResult<TodoDetail?>(null);

        public Task<TodoMutationOutcome> CreateTodoAsync(CreateTodoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoMutationOutcome> UpdateTodoAsync(UpdateTodoCommand command, CancellationToken cancellationToken = default)
            => OnUpdateTodoAsync?.Invoke(command, cancellationToken)
               ?? Task.FromException<TodoMutationOutcome>(new InvalidOperationException("UpdateTodoAsync not configured."));
        public Task<TodoMutationOutcome> DeleteTodoAsync(DeleteTodoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TodoRequirementsAnalysis> AnalyzeTodoRequirementsAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TodoPromptOutput> GenerateTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default)
            => OnGenerateStatusPromptAsync?.Invoke(todoId, cancellationToken)
               ?? Task.FromException<TodoPromptOutput>(new InvalidOperationException("Status prompt not configured."));

        public Task<TodoPromptOutput> GenerateTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default)
            => OnGenerateImplementPromptAsync?.Invoke(todoId, cancellationToken)
               ?? Task.FromException<TodoPromptOutput>(new InvalidOperationException("Implement prompt not configured."));

        public Task<TodoPromptOutput> GenerateTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default)
            => OnGeneratePlanPromptAsync?.Invoke(todoId, cancellationToken)
               ?? Task.FromException<TodoPromptOutput>(new InvalidOperationException("Plan prompt not configured."));

        public IAsyncEnumerable<string> StreamTodoStatusPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<string> StreamTodoImplementPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<string> StreamTodoPlanPromptAsync(string todoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class HealthApiClientStub : IHealthApiClient
    {
        public Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthSnapshot(DateTimeOffset.UtcNow, "healthy", """{"status":"healthy"}"""));
    }
}
