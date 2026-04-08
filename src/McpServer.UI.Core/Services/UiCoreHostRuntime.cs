using System;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.UI.Core.Services;

public sealed class UiCoreHostRuntime : IDisposable
{
    private readonly bool _ownsServices;

    public UiCoreHostRuntime(ServiceProvider services, WorkspaceContextViewModel workspaceContext, bool ownsServices = false)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        WorkspaceContext = workspaceContext ?? throw new ArgumentNullException(nameof(workspaceContext));
        _ownsServices = ownsServices;
    }

    public WorkspaceContextViewModel WorkspaceContext { get; }

    public ServiceProvider Services { get; }

    public T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose()
    {
        if (_ownsServices)
            Services.Dispose();
    }
}
