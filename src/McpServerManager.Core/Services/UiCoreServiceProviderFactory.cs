using System;
using McpServer.Cqrs;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
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
        WorkspaceContextViewModel? workspaceContext = null,
        McpServer.Client.McpServerClient? mcpClient = null,
        Uri? mcpBaseUrl = null)
    {
        if (todoService is null && workspaceService is null)
            throw new ArgumentException("At least one MCP service is required to build the UI.Core host provider.");

        var services = new ServiceCollection();
        services.AddMcpServerManagerUiCore(
            commandTarget,
            todoService,
            workspaceService,
            voiceService,
            sessionLogService,
            eventStreamService,
            fileSystemService,
            processLauncherService,
            timerService,
            jsonParsingService,
            fileSystemWatcherService,
            workspaceContext,
            mcpClient,
            mcpBaseUrl);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .AddProvider(serviceProvider.GetRequiredService<Dispatcher>());
        return serviceProvider;
    }
}
