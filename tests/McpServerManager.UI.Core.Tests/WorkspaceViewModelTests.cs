using McpServerManager.UI.Core;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

/// <summary>Focused ViewModel tests for the shared workspace relay surfaces.</summary>
public sealed class WorkspaceViewModelTests
{
    [Fact]
    public async Task WorkspaceGlobalPromptViewModel_LoadAsync_PopulatesEditor()
    {
        var apiClient = Substitute.For<IWorkspaceApiClient>();
        apiClient.GetWorkspaceGlobalPromptAsync(Arg.Any<CancellationToken>())
            .Returns(new WorkspaceGlobalPromptState("Global prompt", false));

        using var sp = BuildProvider(apiClient);
        var vm = sp.GetRequiredService<WorkspaceGlobalPromptViewModel>();

        await vm.LoadAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.Equal("Global prompt", vm.TemplateText);
        Assert.False(vm.IsDefault);
    }

    [Fact]
    public async Task WorkspaceHealthProbeViewModel_CheckHealthAsync_PopulatesHealthState()
    {
        var apiClient = Substitute.For<IWorkspaceApiClient>();
        apiClient.CheckWorkspaceHealthAsync("E:\\github\\RequestTracker", Arg.Any<CancellationToken>())
            .Returns(new WorkspaceHealthState(true, 200, "http://localhost:7147/health", "{\"status\":\"Healthy\"}", null));

        using var sp = BuildProvider(apiClient);
        var context = sp.GetRequiredService<WorkspaceContextViewModel>();
        context.ActiveWorkspacePath = "E:\\github\\RequestTracker";

        var vm = sp.GetRequiredService<WorkspaceHealthProbeViewModel>();
        await vm.CheckHealthAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.LastHealthState);
        Assert.True(vm.LastHealthState!.Success);
        Assert.Contains("Healthy", vm.HealthStatusText);
    }

    [Fact]
    public async Task WorkspaceDetailViewModel_CreateAsync_SetsLoadedDetail()
    {
        var apiClient = Substitute.For<IWorkspaceApiClient>();
        apiClient.CreateWorkspaceAsync(Arg.Any<CreateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new WorkspaceMutationOutcome(true, null, CreateWorkspaceDetail()));

        using var sp = BuildProvider(apiClient);
        var vm = sp.GetRequiredService<WorkspaceDetailViewModel>();
        vm.BeginNewDraft();
        vm.EditorWorkspacePath = "E:\\github\\RequestTracker";
        vm.EditorName = "RequestTracker";
        vm.EditorTodoPath = "docs\\todo.yaml";

        await vm.CreateAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.Detail);
        Assert.Equal("E:\\github\\RequestTracker", vm.WorkspacePath);
        Assert.False(vm.IsNewDraft);
    }

    [Fact]
    public void WorkspacePolicyViewModel_LoadFromDetail_SeedsAndClearsPolicyLists()
    {
        var apiClient = Substitute.For<IWorkspaceApiClient>();

        using var sp = BuildProvider(apiClient);
        sp.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = "E:\\github\\Active";
        var vm = sp.GetRequiredService<WorkspacePolicyViewModel>();
        var detail = CreateWorkspaceDetail(
            bannedLicenses: ["GPL-3.0"],
            bannedCountriesOfOrigin: ["KP"],
            bannedOrganizations: ["Contoso"],
            bannedIndividuals: ["Mallory"]);

        vm.LoadFromDetail(detail);

        Assert.Equal(["GPL-3.0"], vm.BannedLicenses);
        Assert.Equal(["KP"], vm.BannedCountriesOfOrigin);
        Assert.Equal(["Contoso"], vm.BannedOrganizations);
        Assert.Equal(["Mallory"], vm.BannedIndividuals);
        Assert.Equal("E:\\github\\RequestTracker", vm.WorkspacePath);

        vm.ClearPolicy();

        Assert.Empty(vm.BannedLicenses);
        Assert.Empty(vm.BannedCountriesOfOrigin);
        Assert.Empty(vm.BannedOrganizations);
        Assert.Empty(vm.BannedIndividuals);
        Assert.Equal("E:\\github\\Active", vm.WorkspacePath);
    }

    [Fact]
    public async Task WorkspaceDetailViewModel_SaveAsync_SendsPromptOverrideFields()
    {
        var apiClient = Substitute.For<IWorkspaceApiClient>();
        apiClient.UpdateWorkspaceAsync(Arg.Any<UpdateWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<UpdateWorkspaceCommand>();
                return new WorkspaceMutationOutcome(
                    true,
                    null,
                    CreateWorkspaceDetail(
                        promptTemplate: command.PromptTemplate,
                        statusPrompt: command.StatusPrompt ?? string.Empty,
                        implementPrompt: command.ImplementPrompt ?? string.Empty,
                        planPrompt: command.PlanPrompt ?? string.Empty));
            });

        using var sp = BuildProvider(apiClient);
        var vm = sp.GetRequiredService<WorkspaceDetailViewModel>();
        vm.WorkspacePath = "E:\\github\\RequestTracker";
        vm.EditorName = "RequestTracker";
        vm.EditorTodoPath = "docs\\todo.yaml";
        vm.EditorPromptTemplateText = "Prompt template";
        vm.EditorStatusPromptText = "Status prompt";
        vm.EditorImplementPromptText = "Implement prompt";
        vm.EditorPlanPromptText = "Plan prompt";
        vm.IsNewDraft = false;

        await vm.SaveAsync();

        await apiClient.Received(1).UpdateWorkspaceAsync(
            Arg.Is<UpdateWorkspaceCommand>(command =>
                command.WorkspacePath == "E:\\github\\RequestTracker"
                && command.PromptTemplate == "Prompt template"
                && command.StatusPrompt == "Status prompt"
                && command.ImplementPrompt == "Implement prompt"
                && command.PlanPrompt == "Plan prompt"),
            Arg.Any<CancellationToken>());
    }

    private static ServiceProvider BuildProvider(IWorkspaceApiClient apiClient)
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
        services.AddCqrs(typeof(WorkspaceViewModelTests).Assembly);
        services.AddUiCore();
        return services.BuildServiceProvider();
    }

    private static WorkspaceDetail CreateWorkspaceDetail(
        string workspacePath = "E:\\github\\RequestTracker",
        string name = "RequestTracker",
        string todoPath = "docs\\todo.yaml",
        string? dataDirectory = "data",
        string? tunnelProvider = "ngrok",
        bool isPrimary = false,
        bool isEnabled = true,
        string? runAs = null,
        string? promptTemplate = "Prompt template",
        string statusPrompt = "Status prompt",
        string implementPrompt = "Implement prompt",
        string planPrompt = "Plan prompt",
        IReadOnlyList<string>? bannedLicenses = null,
        IReadOnlyList<string>? bannedCountriesOfOrigin = null,
        IReadOnlyList<string>? bannedOrganizations = null,
        IReadOnlyList<string>? bannedIndividuals = null)
        => new(
            WorkspacePath: workspacePath,
            Name: name,
            TodoPath: todoPath,
            DataDirectory: dataDirectory,
            TunnelProvider: tunnelProvider,
            IsPrimary: isPrimary,
            IsEnabled: isEnabled,
            RunAs: runAs,
            PromptTemplate: promptTemplate,
            StatusPrompt: statusPrompt,
            ImplementPrompt: implementPrompt,
            PlanPrompt: planPrompt,
            DateTimeCreated: DateTimeOffset.Parse("2026-03-01T00:00:00Z"),
            DateTimeModified: DateTimeOffset.Parse("2026-03-02T00:00:00Z"),
            BannedLicenses: bannedLicenses ?? [],
            BannedCountriesOfOrigin: bannedCountriesOfOrigin ?? [],
            BannedOrganizations: bannedOrganizations ?? [],
            BannedIndividuals: bannedIndividuals ?? []);
}
