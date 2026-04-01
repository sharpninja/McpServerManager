using McpServerManager.UI.Core;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Reflection;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

public sealed class TodoViewModelTests
{
    [Fact]
    public async Task TodoDetailViewModel_CreateAsync_PreservesExtendedFields()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        apiClient.CreateTodoAsync(Arg.Any<CreateTodoCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<CreateTodoCommand>()!;
                return new TodoMutationOutcome(
                    true,
                    null,
                    new TodoDetail(
                        command.Id,
                        command.Title,
                        command.Section,
                        command.Priority,
                        false,
                        command.Estimate,
                        command.Note,
                        command.Description?.ToList() ?? [],
                        command.TechnicalDetails?.ToList() ?? [],
                        command.ImplementationTasks?.ToList() ?? [],
                        null,
                        null,
                        command.Remaining,
                        null,
                        null,
                        command.DependsOn?.ToList() ?? [],
                        command.FunctionalRequirements?.ToList() ?? [],
                        command.TechnicalRequirements?.ToList() ?? [],
                        command.Phase));
            });

        using var sp = BuildProvider(apiClient);
        var vm = sp.GetRequiredService<TodoDetailViewModel>();
        vm.BeginNewDraft();
        vm.EditorId = "TODO-123";
        vm.EditorTitle = "Title";
        vm.EditorSection = "general";
        vm.EditorPriority = "high";
        vm.EditorEstimate = "2h";
        vm.EditorNote = "Note";
        vm.EditorRemaining = "Remaining";
        vm.EditorPhase = "phase-4";
        vm.EditorDescriptionText = "Line one\nLine two";
        vm.EditorTechnicalDetailsText = "Detail one";
        vm.EditorImplementationTasksText = "[x] Finished task";
        vm.EditorDependsOnText = "TODO-001";
        vm.EditorFunctionalRequirementsText = "FR-001";
        vm.EditorTechnicalRequirementsText = "TR-001";

        await vm.CreateAsync();

        await apiClient.Received(1).CreateTodoAsync(
            Arg.Is<CreateTodoCommand>(cmd =>
                cmd != null &&
                cmd.Id == "TODO-123" &&
                cmd.Note == "Note" &&
                cmd.Remaining == "Remaining" &&
                cmd.Phase == "phase-4" &&
                cmd.DependsOn!.SequenceEqual(new[] { "TODO-001" }) &&
                cmd.FunctionalRequirements!.SequenceEqual(new[] { "FR-001" }) &&
                cmd.TechnicalRequirements!.SequenceEqual(new[] { "TR-001" })),
            Arg.Any<CancellationToken>());

        Assert.Null(vm.ErrorMessage);
        Assert.Equal("TODO-123", vm.Detail?.Id);
        Assert.Equal("phase-4", vm.Detail?.Phase);
    }

    [Fact]
    public async Task TodoDetailViewModel_SaveAsync_PreservesExtendedFields()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        apiClient.UpdateTodoAsync(Arg.Any<UpdateTodoCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<UpdateTodoCommand>()!;
                return new TodoMutationOutcome(
                    true,
                    null,
                    new TodoDetail(
                        command.TodoId,
                        command.Title ?? "Title",
                        command.Section ?? "general",
                        command.Priority ?? "medium",
                        command.Done ?? false,
                        command.Estimate,
                        command.Note,
                        command.Description?.ToList() ?? [],
                        command.TechnicalDetails?.ToList() ?? [],
                        command.ImplementationTasks?.ToList() ?? [],
                        command.CompletedDate,
                        command.DoneSummary,
                        command.Remaining,
                        null,
                        null,
                        command.DependsOn?.ToList() ?? [],
                        command.FunctionalRequirements?.ToList() ?? [],
                        command.TechnicalRequirements?.ToList() ?? [],
                        command.Phase));
            });

        using var sp = BuildProvider(apiClient);
        var vm = sp.GetRequiredService<TodoDetailViewModel>();
        vm.IsNewDraft = false;
        vm.EditorId = "TODO-123";
        vm.EditorTitle = "Title";
        vm.EditorSection = "general";
        vm.EditorPriority = "high";
        vm.EditorDone = true;
        vm.EditorEstimate = "2h";
        vm.EditorNote = "Note";
        vm.EditorCompletedDate = "2026-03-03";
        vm.EditorDoneSummary = "Done";
        vm.EditorRemaining = "Remaining";
        vm.EditorPhase = "phase-6";
        vm.EditorDescriptionText = "Line one\nLine two";
        vm.EditorTechnicalDetailsText = "Detail one";
        vm.EditorImplementationTasksText = "[x] Finished task";
        vm.EditorDependsOnText = "TODO-001";
        vm.EditorFunctionalRequirementsText = "FR-001";
        vm.EditorTechnicalRequirementsText = "TR-001";

        await vm.SaveAsync();

        await apiClient.Received(1).UpdateTodoAsync(
            Arg.Is<UpdateTodoCommand>(cmd =>
                cmd != null &&
                cmd.TodoId == "TODO-123" &&
                cmd.CompletedDate == "2026-03-03" &&
                cmd.DoneSummary == "Done" &&
                cmd.Remaining == "Remaining" &&
                cmd.Phase == "phase-6" &&
                cmd.DependsOn!.SequenceEqual(new[] { "TODO-001" }) &&
                cmd.FunctionalRequirements!.SequenceEqual(new[] { "FR-001" }) &&
                cmd.TechnicalRequirements!.SequenceEqual(new[] { "TR-001" })),
            Arg.Any<CancellationToken>());

        Assert.Null(vm.ErrorMessage);
        Assert.Equal("TODO-123", vm.Detail?.Id);
        Assert.Equal("2026-03-03", vm.EditorCompletedDate);
        Assert.Equal("phase-6", vm.EditorPhase);
    }

    [Fact]
    public void TodoListHostViewModel_CreateScratchDetailVm_FallsBackToPrimaryVm_WhenScratchResolutionFails()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        using var sp = BuildProvider(apiClient);

        var primaryDetailVm = sp.GetRequiredService<TodoDetailViewModel>();
        var host = new TodoListHostViewModel(
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<TodoListViewModel>(),
            primaryDetailVm,
            sp.GetRequiredService<WorkspaceContextViewModel>(),
            new NullServiceProvider(),
            new NoOpTimerService(),
            sp.GetRequiredService<ILogger<TodoListHostViewModel>>());

        var createScratch = typeof(TodoListHostViewModel)
            .GetMethod("CreateScratchDetailVm", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(createScratch);
        var scratch = createScratch!.Invoke(host, null);
        Assert.Same(primaryDetailVm, scratch);
    }

    [Fact]
    public void TodoListHostViewModel_StatusTextError_EmitsWarningLog()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        using var sp = BuildProvider(apiClient);

        var logger = new RecordingLogger<TodoListHostViewModel>();
        var host = new TodoListHostViewModel(
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<TodoListViewModel>(),
            sp.GetRequiredService<TodoDetailViewModel>(),
            sp.GetRequiredService<WorkspaceContextViewModel>(),
            sp,
            new NoOpTimerService(),
            logger);

        host.StatusText = "Error: simulated todo failure";

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("Todo status update", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TodoListHostViewModel_CopilotStatusCommand_SelectsProvidedEntry()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        using var sp = BuildProvider(apiClient);

        var host = CreateTrackingHost(sp);
        var entry = CreateTodoListEntry("TODO-STATUS-001");

        await host.CopilotStatusCommand.ExecuteAsync(entry);

        Assert.Same(entry, host.SelectedEntry);
        Assert.Equal(1, host.StatusCalls);
    }

    [Fact]
    public async Task TodoListHostViewModel_CopilotPlanCommand_SelectsProvidedEntry()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        using var sp = BuildProvider(apiClient);

        var host = CreateTrackingHost(sp);
        var entry = CreateTodoListEntry("TODO-PLAN-001");

        await host.CopilotPlanCommand.ExecuteAsync(entry);

        Assert.Same(entry, host.SelectedEntry);
        Assert.Equal(1, host.PlanCalls);
    }

    [Fact]
    public async Task TodoListHostViewModel_CopilotImplementCommand_SelectsProvidedEntry()
    {
        var apiClient = Substitute.For<ITodoApiClient>();
        using var sp = BuildProvider(apiClient);

        var host = CreateTrackingHost(sp);
        var entry = CreateTodoListEntry("TODO-IMPLEMENT-001");

        await host.CopilotImplementCommand.ExecuteAsync(entry);

        Assert.Same(entry, host.SelectedEntry);
        Assert.Equal(1, host.ImplementCalls);
    }

    private static ServiceProvider BuildProvider(ITodoApiClient apiClient)
    {
        var auth = Substitute.For<IAuthorizationPolicyService>();
        auth.CanExecuteAction(Arg.Any<string>()).Returns(true);

        var health = Substitute.For<IHealthApiClient>();
        health.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthSnapshot(DateTimeOffset.UtcNow, "healthy", "{}"));

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(apiClient);
        services.AddSingleton(health);
        services.AddSingleton(auth);
        services.AddCqrs(typeof(TodoViewModelTests).Assembly);
        services.AddUiCore();
        return services.BuildServiceProvider();
    }

    private static TrackingTodoListHostViewModel CreateTrackingHost(ServiceProvider sp)
        => new(
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<TodoListViewModel>(),
            sp.GetRequiredService<TodoDetailViewModel>(),
            sp.GetRequiredService<WorkspaceContextViewModel>(),
            sp,
            sp.GetRequiredService<ITimerService>(),
            sp.GetRequiredService<ILogger<TodoListHostViewModel>>());

    private static TodoListEntry CreateTodoListEntry(string id)
        => new()
        {
            PriorityGroup = "High",
            DisplayLine = $"{id} Sample TODO",
            Item = new McpTodoFlatItem
            {
                Id = id,
                Title = "Sample TODO",
                Section = "general",
                Priority = "high"
            }
        };

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TrackingTodoListHostViewModel : TodoListHostViewModel
    {
        public TrackingTodoListHostViewModel(
            IClipboardService clipboardService,
            TodoListViewModel listVm,
            TodoDetailViewModel detailVm,
            WorkspaceContextViewModel workspaceContext,
            IServiceProvider serviceProvider,
            ITimerService timerService,
            ILogger<TodoListHostViewModel> logger)
            : base(clipboardService, listVm, detailVm, workspaceContext, serviceProvider, timerService, logger)
        {
        }

        public int StatusCalls { get; private set; }
        public int PlanCalls { get; private set; }
        public int ImplementCalls { get; private set; }

        public override Task CopilotStatusAsync()
        {
            StatusCalls++;
            return Task.CompletedTask;
        }

        public override Task CopilotPlanAsync()
        {
            PlanCalls++;
            return Task.CompletedTask;
        }

        public override Task CopilotImplementAsync()
        {
            ImplementCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Entries.Add((logLevel, message));
        }
    }
}
