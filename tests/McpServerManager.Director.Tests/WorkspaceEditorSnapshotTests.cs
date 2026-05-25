using McpServer.Cqrs;
using McpServerManager.Director.Screens;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace McpServerManager.Director.Tests;

public sealed class WorkspaceEditorSnapshotTests
{
    [Fact]
    public void ApplyTo_CopiesAllEditableWorkspaceFields()
    {
        using var sp = BuildProvider();
        var vm = sp.GetRequiredService<WorkspaceDetailViewModel>();
        var snapshot = new WorkspaceEditorSnapshot(
            @"C:\repo",
            "Repo",
            "docs/todo.yaml",
            "data",
            "ngrok",
            "DOMAIN\\svc",
            true,
            false,
            "prompt",
            "status",
            "implement",
            "plan");

        snapshot.ApplyTo(vm);

        Assert.Equal(@"C:\repo", vm.EditorWorkspacePath);
        Assert.Equal("Repo", vm.EditorName);
        Assert.Equal("docs/todo.yaml", vm.EditorTodoPath);
        Assert.Equal("data", vm.EditorDataDirectory);
        Assert.Equal("ngrok", vm.EditorTunnelProvider);
        Assert.Equal("DOMAIN\\svc", vm.EditorRunAs);
        Assert.True(vm.EditorIsPrimary);
        Assert.False(vm.EditorIsEnabled);
        Assert.Equal("prompt", vm.EditorPromptTemplateText);
        Assert.Equal("status", vm.EditorStatusPromptText);
        Assert.Equal("implement", vm.EditorImplementPromptText);
        Assert.Equal("plan", vm.EditorPlanPromptText);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void FromViewModel_CapturesAllEditableWorkspaceFields()
    {
        using var sp = BuildProvider();
        var vm = sp.GetRequiredService<WorkspaceDetailViewModel>();
        vm.EditorWorkspacePath = @"C:\repo";
        vm.EditorName = "Repo";
        vm.EditorTodoPath = "docs/todo.yaml";
        vm.EditorDataDirectory = "data";
        vm.EditorTunnelProvider = "ngrok";
        vm.EditorRunAs = "DOMAIN\\svc";
        vm.EditorIsPrimary = true;
        vm.EditorIsEnabled = false;
        vm.EditorPromptTemplateText = "prompt";
        vm.EditorStatusPromptText = "status";
        vm.EditorImplementPromptText = "implement";
        vm.EditorPlanPromptText = "plan";

        var snapshot = WorkspaceEditorSnapshot.FromViewModel(vm);

        Assert.Equal(@"C:\repo", snapshot.WorkspacePath);
        Assert.Equal("Repo", snapshot.Name);
        Assert.Equal("docs/todo.yaml", snapshot.TodoPath);
        Assert.Equal("data", snapshot.DataDirectory);
        Assert.Equal("ngrok", snapshot.TunnelProvider);
        Assert.Equal("DOMAIN\\svc", snapshot.RunAs);
        Assert.True(snapshot.IsPrimary);
        Assert.False(snapshot.IsEnabled);
        Assert.Equal("prompt", snapshot.PromptTemplate);
        Assert.Equal("status", snapshot.StatusPrompt);
        Assert.Equal("implement", snapshot.ImplementPrompt);
        Assert.Equal("plan", snapshot.PlanPrompt);
    }

    private static ServiceProvider BuildProvider()
    {
        var workspaceApi = Substitute.For<IWorkspaceApiClient>();
        var healthApi = Substitute.For<IHealthApiClient>();
        var authorization = Substitute.For<IAuthorizationPolicyService>();
        authorization.CanExecuteAction(Arg.Any<string>()).Returns(true);
        healthApi.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthSnapshot(DateTimeOffset.UtcNow, "Healthy", "{}"));

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(workspaceApi);
        services.AddSingleton(healthApi);
        services.AddSingleton(authorization);
        services.AddCqrs(typeof(WorkspaceEditorSnapshotTests).Assembly);
        services.AddUiCore();
        return services.BuildServiceProvider();
    }
}
