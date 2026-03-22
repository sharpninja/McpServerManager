using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Director;

/// <summary>
/// Shared Director composition root for all short-lived provider entry points.
/// </summary>
internal static class DirectorHost
{
    /// <summary>
    /// Creates a fully configured Director service provider for the requested workspace.
    /// </summary>
    /// <param name="workspace">Optional workspace path override.</param>
    /// <param name="additionalConfig">
    /// Optional callback for entry-point-specific registrations that must layer on top
    /// of the shared Director graph before the provider is built.
    /// </param>
    /// <returns>The built Director service provider.</returns>
    public static ServiceProvider CreateProvider(
        string? workspace = null,
        Action<IServiceCollection, DirectorMcpContext>? additionalConfig = null)
    {
        var services = new ServiceCollection();
        var directorContext = DirectorServiceRegistration.Configure(services, workspace);
        additionalConfig?.Invoke(services, directorContext);
        return services.BuildServiceProvider();
    }
}
