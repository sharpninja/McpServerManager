using System;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

internal sealed class UiCoreAppRuntime : IDisposable
{
    public UiCoreAppRuntime(
        McpServerManager.Core.Commands.ICommandTarget commandTarget,
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
        WorkspaceContext = workspaceContext ?? new WorkspaceContextViewModel();
        Services = UiCoreServiceProviderFactory.Build(
            commandTarget,
            todoService, workspaceService, voiceService,
            sessionLogService, eventStreamService,
            fileSystemService, processLauncherService,
            timerService, jsonParsingService, fileSystemWatcherService,
            WorkspaceContext, mcpClient, mcpBaseUrl);
    }

    public WorkspaceContextViewModel WorkspaceContext { get; }

    public ServiceProvider Services { get; }

    public T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose() => Services.Dispose();
}
