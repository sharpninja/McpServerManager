using System;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServer.UI.Core.Services;
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
        
        // Register a simple synchronous UI dispatcher for testing
        services.AddSingleton<IUiDispatcherService>(_ => new SynchronousUiDispatcher());
        
        services.AddCqrsDispatcher();
        services.AddCqrsHandlers(typeof(Commands.NavigateBackCommand).Assembly);
        // Relay commands dispatch InvokeUiActionCommand from UI.Core; register that handler assembly too.
        services.AddCqrsHandlers(typeof(McpServer.UI.Core.Commands.InvokeUiActionHandler).Assembly);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Simple synchronous UI dispatcher for testing that executes actions immediately on the current thread.
    /// </summary>
    private sealed class SynchronousUiDispatcher : IUiDispatcherService
    {
        public bool CheckAccess() => true;

        public Task InvokeAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return action();
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }
    }
}
