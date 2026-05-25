using System;
using McpServer.Client;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Hosting;

namespace McpServerManager.UI.Core.Services;

public sealed class MainWindowHostServices
{
    public MainWindowHostServices(
        string baseUrl,
        string? apiKey,
        string? bearerToken,
        IHostIdentityProvider identityProvider,
        IMcpHostContext hostContext,
        McpServerClient client,
        McpTodoService todoService,
        McpWorkspaceService workspaceService,
        McpVoiceConversationService voiceService,
        McpSessionLogService sessionLogService,
        McpAgentEventStreamService eventStreamService,
        UiCoreHostRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(identityProvider);
        ArgumentNullException.ThrowIfNull(hostContext);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(todoService);
        ArgumentNullException.ThrowIfNull(workspaceService);
        ArgumentNullException.ThrowIfNull(voiceService);
        ArgumentNullException.ThrowIfNull(sessionLogService);
        ArgumentNullException.ThrowIfNull(eventStreamService);
        ArgumentNullException.ThrowIfNull(runtime);

        BaseUrl = baseUrl;
        ApiKey = apiKey;
        BearerToken = bearerToken;
        IdentityProvider = identityProvider;
        HostContext = hostContext;
        Client = client;
        TodoService = todoService;
        WorkspaceService = workspaceService;
        VoiceService = voiceService;
        SessionLogService = sessionLogService;
        EventStreamService = eventStreamService;
        Runtime = runtime;
    }

    public string BaseUrl { get; }

    public string? ApiKey { get; }

    public string? BearerToken { get; }

    public IHostIdentityProvider IdentityProvider { get; }

    public IMcpHostContext HostContext { get; }

    public McpServerClient Client { get; }

    public McpTodoService TodoService { get; }

    public McpWorkspaceService WorkspaceService { get; }

    public McpVoiceConversationService VoiceService { get; }

    public McpSessionLogService SessionLogService { get; }

    public McpAgentEventStreamService EventStreamService { get; }

    public UiCoreHostRuntime Runtime { get; }
}
