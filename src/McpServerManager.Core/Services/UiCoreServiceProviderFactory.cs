using System;
using McpServer.Cqrs;
using McpServer.UI.Core;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using McpServerManager.Core.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

internal static class UiCoreServiceProviderFactory
{
    public static ServiceProvider Build(
        Commands.ICommandTarget commandTarget,
        McpTodoService? todoService = null,
        McpWorkspaceService? workspaceService = null,
        McpVoiceConversationService? voiceService = null,
        McpSessionLogService? sessionLogService = null,
        McpAgentEventStreamService? eventStreamService = null,
        IFileSystemService? fileSystemService = null,
        IProcessLauncherService? processLauncherService = null,
        ITimerService? timerService = null,
        IJsonParsingService? jsonParsingService = null,
        IFileSystemWatcherService? fileSystemWatcherService = null,
        WorkspaceContextViewModel? workspaceContext = null)
    {
        if (todoService is null && workspaceService is null)
            throw new ArgumentException("At least one MCP service is required to build the UI.Core host provider.");

        var services = new ServiceCollection();
        services.AddSingleton(AppLogService.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(AppLogger<>));

        if (todoService is not null)
            services.AddSingleton<ITodoApiClient>(_ => new UiCoreTodoApiClientAdapter(todoService));

        if (workspaceService is not null)
            services.AddSingleton<IWorkspaceApiClient>(_ => new UiCoreWorkspaceApiClientAdapter(workspaceService));

        if (voiceService is not null)
            services.AddSingleton<IVoiceApiClient>(_ => new UiCoreVoiceApiClientAdapter(voiceService));

        if (sessionLogService is not null)
            services.AddSingleton<ISessionLogApiClient>(_ => new UiCoreSessionLogApiClientAdapter(sessionLogService));

        if (eventStreamService is not null)
            services.AddSingleton<IEventStreamApiClient>(_ => new UiCoreEventStreamApiClientAdapter(eventStreamService));

        // Infrastructure services — use host-provided or default implementations
        services.AddSingleton(fileSystemService ?? new FileSystemService());
        services.AddSingleton(processLauncherService ?? new ProcessLauncherService());
        services.AddSingleton(timerService ?? new TimerService());
        services.AddSingleton(jsonParsingService ?? new JsonParsingService());
        services.AddSingleton(fileSystemWatcherService ?? new FileSystemWatcherService());

        // Register ICommandTarget for CQRS handler DI
        services.AddSingleton<Commands.ICommandTarget>(commandTarget);

        services.AddCqrsDispatcher();
        // Scan app command handlers for DI-based handler discovery
        services.AddCqrsHandlers(typeof(Commands.NavigateBackCommand).Assembly);
        services.AddUiCore();
        if (workspaceContext is not null)
            services.AddSingleton(workspaceContext);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .AddProvider(serviceProvider.GetRequiredService<Dispatcher>());
        return serviceProvider;
    }
}
