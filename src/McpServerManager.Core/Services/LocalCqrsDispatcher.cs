using System;
using McpServer.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

public static class LocalCqrsDispatcher
{
    private static readonly Lazy<ServiceProvider> Provider = new(BuildProvider, isThreadSafe: true);

    public static Dispatcher Instance => Provider.Value.GetRequiredService<Dispatcher>();

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(AppLogService.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(AppLogger<>));
        services.AddCqrsDispatcher();
        services.AddCqrsHandlers(typeof(Commands.NavigateBackCommand).Assembly);
        // Relay commands dispatch InvokeUiActionCommand from UI.Core; register that handler assembly too.
        services.AddCqrsHandlers(typeof(McpServer.UI.Core.Commands.InvokeUiActionHandler).Assembly);
        return services.BuildServiceProvider();
    }
}
