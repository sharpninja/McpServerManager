using System.Linq;
using System.Reflection;
using McpServerManager.UI.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpServerManager.Desktop.Tests;

/// <summary>
/// Regression guard for the "workspace dropdown not populating" bug: the Desktop factory's
/// RegisterUiCoreCommandTargets must register EVERY UI.Core command target aggregated by
/// <see cref="ICommandTarget"/>. The workspace targets (IWorkspaceSwitchTarget,
/// ILoadWorkspaceConnectionsTarget, IWorkspaceHealthTarget) were previously omitted, so their command
/// handlers could not resolve their target and the workspace load failed silently.
/// </summary>
public sealed class DesktopCommandTargetRegistrationTests
{
    /// <summary>Every interface aggregated by ICommandTarget is registered by the Desktop factory.</summary>
    [Fact]
    public void RegisterUiCoreCommandTargets_CoversEveryAggregatedCommandTarget()
    {
        var services = new ServiceCollection();

        var method = typeof(McpServerManager.Desktop.Services.DesktopAppServiceFactory)
            .GetMethod("RegisterUiCoreCommandTargets", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method!.Invoke(null, new object[] { services });

        var registered = services.Select(d => d.ServiceType).ToHashSet();

        foreach (var target in typeof(ICommandTarget).GetInterfaces())
            Assert.True(registered.Contains(target), $"UI.Core command target not registered by Desktop factory: {target.Name}");

        Assert.Contains(typeof(ICommandTarget), registered);
        // Explicit checks for the previously-missing workspace targets.
        Assert.Contains(typeof(ILoadWorkspaceConnectionsTarget), registered);
        Assert.Contains(typeof(IWorkspaceSwitchTarget), registered);
        Assert.Contains(typeof(IWorkspaceHealthTarget), registered);
    }
}
