using System;
using System.Reflection;
using McpServer.Cqrs;
using McpServer.UI.Core;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using McpServerManager.Core.Services;
using McpServerManager.Core.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core;

/// <summary>
/// DI registration extensions for McpServerManager.Core hosts.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full McpServerManager.Core + McpServer.UI.Core stack in one call.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="commandTarget">UI command target used by CQRS command handlers.</param>
    /// <param name="todoService">Optional TODO API adapter.</param>
    /// <param name="workspaceService">Optional workspace API adapter.</param>
    /// <param name="voiceService">Optional voice API adapter.</param>
    /// <param name="sessionLogService">Optional session log API adapter.</param>
    /// <param name="eventStreamService">Optional event stream API adapter.</param>
    /// <param name="fileSystemService">Optional file-system service override.</param>
    /// <param name="processLauncherService">Optional process-launcher service override.</param>
    /// <param name="timerService">Optional timer service override.</param>
    /// <param name="jsonParsingService">Optional JSON parser service override.</param>
    /// <param name="fileSystemWatcherService">Optional file watcher service override.</param>
    /// <param name="workspaceContext">Optional shared workspace context instance.</param>
    /// <param name="mcpClient">Optional MCP client used by health adapter.</param>
    /// <param name="mcpBaseUrl">Optional MCP base URL used by health adapter.</param>
    /// <param name="additionalViewModelAssemblies">Additional assemblies to scan for ViewModels.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="commandTarget"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="todoService"/> and <paramref name="workspaceService"/> are null.</exception>
    public static IServiceCollection AddMcpServerManagerUiCore(
        this IServiceCollection services,
        McpServerManager.Core.Commands.ICommandTarget commandTarget,
        McpServerManager.Core.Services.McpTodoService? todoService = null,
        McpServerManager.Core.Services.McpWorkspaceService? workspaceService = null,
        McpServerManager.Core.Services.McpVoiceConversationService? voiceService = null,
        McpServerManager.Core.Services.McpSessionLogService? sessionLogService = null,
        McpServerManager.Core.Services.McpAgentEventStreamService? eventStreamService = null,
        IFileSystemService? fileSystemService = null,
        IProcessLauncherService? processLauncherService = null,
        ITimerService? timerService = null,
        IJsonParsingService? jsonParsingService = null,
        IFileSystemWatcherService? fileSystemWatcherService = null,
        WorkspaceContextViewModel? workspaceContext = null,
        McpServer.Client.McpServerClient? mcpClient = null,
        Uri? mcpBaseUrl = null,
        params Assembly[] additionalViewModelAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandTarget);

        if (todoService is null && workspaceService is null)
            throw new ArgumentException("At least one MCP service is required to build the UI.Core host provider.");

        services.AddSingleton(McpServerManager.Core.Services.AppLogService.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<McpServerManager.Core.Services.AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(McpServerManager.Core.Services.AppLogger<>));

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

        if (mcpClient is not null)
            services.AddSingleton<IHealthApiClient>(_ => new UiCoreHealthApiClientAdapter(mcpClient, mcpBaseUrl));

        // Infrastructure services — use host-provided or default implementations
        services.AddSingleton<IFileSystemService>(fileSystemService ?? new FileSystemService());
        services.AddSingleton<IProcessLauncherService>(processLauncherService ?? new ProcessLauncherService());
        services.AddSingleton<ITimerService>(timerService ?? new TimerService());
        services.AddSingleton<IJsonParsingService>(jsonParsingService ?? new JsonParsingService());
        services.AddSingleton<IFileSystemWatcherService>(fileSystemWatcherService ?? new FileSystemWatcherService());
        services.AddSingleton<McpServer.UI.Core.Services.IClipboardService>(_ => new McpServer.UI.Core.Services.NoOpClipboardService());
        services.AddSingleton<McpServer.UI.Core.Services.IAppLogService>(_ => McpServerManager.Core.Services.AppLogService.Instance);
        services.AddSingleton<McpServer.UI.Core.Services.ISpeechFilterService>(_ => new SpeechFilterServiceAdapter());
        services.AddSingleton<McpServer.UI.Core.Services.IUiDispatcherService>(_ => new AvaloniaUiDispatcherService());
        services.AddSingleton<McpServer.UI.Core.Services.IConnectionAuthService>(_ => new ConnectionAuthServiceAdapter());

        // Register ICommandTarget and granular sub-interfaces for CQRS handler DI
        services.AddSingleton<McpServerManager.Core.Commands.ICommandTarget>(commandTarget);
        services.AddSingleton<McpServerManager.Core.Commands.INavigationTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IRequestDetailsTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IPreviewTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IArchiveTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.ISessionDataTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IClipboardTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IConfigTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.IUiDispatchTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServer.UI.Core.Commands.IUiDispatchTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServerManager.Core.Commands.ITodoCopilotTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());
        services.AddSingleton<McpServer.UI.Core.Commands.ITodoCopilotTarget>(sp => sp.GetRequiredService<McpServerManager.Core.Commands.ICommandTarget>());

        services.AddCqrsDispatcher();
        services.AddCqrsHandlers(typeof(McpServerManager.Core.Commands.NavigateBackCommand).Assembly);
        services.AddUiCore(additionalViewModelAssemblies);

        if (workspaceContext is not null)
            services.AddSingleton(workspaceContext);

        return services;
    }
}
