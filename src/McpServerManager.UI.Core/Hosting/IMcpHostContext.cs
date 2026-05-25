using McpServer.Client;

namespace McpServerManager.UI.Core.Hosting;

public interface IMcpHostContext
{
    string? ActiveWorkspacePath { get; }

    void RefreshBearerTokens();

    bool TrySetActiveWorkspace(string? workspacePath);

    Task<McpServerClient> GetRequiredControlApiClientAsync(CancellationToken cancellationToken = default);

    Task<McpServerClient> GetRequiredActiveWorkspaceApiClientAsync(CancellationToken cancellationToken = default);
}
