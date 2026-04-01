using McpServer.Client;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Hosting;

public sealed class AvaloniaMcpContext : IMcpHostContext
{
    private readonly object _gate = new();
    private readonly McpServerClient _client;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly IHostIdentityProvider? _identityProvider;

    public AvaloniaMcpContext(
        McpServerClient client,
        WorkspaceContextViewModel workspaceContext,
        IHostIdentityProvider? identityProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(workspaceContext);

        _client = client;
        _workspaceContext = workspaceContext;
        _identityProvider = identityProvider;

        var initialWorkspacePath = string.IsNullOrWhiteSpace(workspaceContext.ActiveWorkspacePath)
            ? NormalizeWorkspacePath(client.WorkspacePath)
            : NormalizeWorkspacePath(workspaceContext.ActiveWorkspacePath);

        ActiveWorkspacePath = initialWorkspacePath;
        ApplyIdentityNoLock();

        if (!string.Equals(_workspaceContext.ActiveWorkspacePath, initialWorkspacePath, StringComparison.Ordinal))
            _workspaceContext.ActiveWorkspacePath = initialWorkspacePath;

        _workspaceContext.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkspaceContextViewModel.ActiveWorkspacePath))
                TrySetActiveWorkspace(_workspaceContext.ActiveWorkspacePath, updateViewModel: false);
        };
    }

    public string? ActiveWorkspacePath { get; private set; }

    public void RefreshBearerTokens()
    {
        lock (_gate)
            ApplyIdentityNoLock();
    }

    public bool TrySetActiveWorkspace(string? workspacePath)
        => TrySetActiveWorkspace(workspacePath, updateViewModel: true);

    public Task<McpServerClient> GetRequiredControlApiClientAsync(CancellationToken cancellationToken = default)
    {
        RefreshBearerTokens();
        return Task.FromResult(_client);
    }

    public Task<McpServerClient> GetRequiredActiveWorkspaceApiClientAsync(CancellationToken cancellationToken = default)
    {
        RefreshBearerTokens();
        return Task.FromResult(_client);
    }

    private bool TrySetActiveWorkspace(string? workspacePath, bool updateViewModel)
    {
        var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);

        lock (_gate)
        {
            ActiveWorkspacePath = normalizedWorkspacePath;
            ApplyIdentityNoLock();
        }

        if (updateViewModel &&
            !string.Equals(_workspaceContext.ActiveWorkspacePath, normalizedWorkspacePath, StringComparison.Ordinal))
        {
            _workspaceContext.ActiveWorkspacePath = normalizedWorkspacePath;
        }

        return true;
    }

    private static string? NormalizeWorkspacePath(string? workspacePath)
        => string.IsNullOrWhiteSpace(workspacePath) ? null : workspacePath.Trim();

    private void ApplyIdentityNoLock()
    {
        var identityWorkspacePath = NormalizeWorkspacePath(_identityProvider?.GetWorkspacePath());
        if (!string.IsNullOrWhiteSpace(identityWorkspacePath))
            ActiveWorkspacePath = identityWorkspacePath;

        if (!string.Equals(_workspaceContext.ActiveWorkspacePath, ActiveWorkspacePath, StringComparison.Ordinal))
            _workspaceContext.ActiveWorkspacePath = ActiveWorkspacePath;

        var workspacePath = ActiveWorkspacePath ?? string.Empty;
        var bearerToken = NormalizeCredential(_identityProvider?.GetBearerToken());
        var apiKey = NormalizeCredential(_identityProvider?.GetApiKey());

        _client.Logout();
        _client.WorkspacePath = workspacePath;

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            _client.BearerToken = bearerToken;
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
            _client.ApiKey = apiKey;
    }

    private static string? NormalizeCredential(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
