using System;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

internal sealed class UiCoreAppRuntime : IDisposable
{
    public UiCoreAppRuntime(
        McpTodoService? todoService = null,
        McpWorkspaceService? workspaceService = null,
        WorkspaceContextViewModel? workspaceContext = null)
    {
        WorkspaceContext = workspaceContext ?? new WorkspaceContextViewModel();
        Services = UiCoreServiceProviderFactory.Build(todoService, workspaceService, WorkspaceContext);
    }

    public WorkspaceContextViewModel WorkspaceContext { get; }

    public ServiceProvider Services { get; }

    public T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose() => Services.Dispose();
}
