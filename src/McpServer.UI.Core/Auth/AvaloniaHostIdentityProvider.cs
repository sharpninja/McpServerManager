using System;

namespace McpServer.UI.Core.Auth;

public sealed class AvaloniaHostIdentityProvider : IMutableHostIdentityProvider
{
    private readonly object _gate = new();
    private Func<string?>? _resolveWorkspacePath;
    private string? _apiKey;
    private string? _bearerToken;

    public AvaloniaHostIdentityProvider(
        string? apiKey = null,
        string? bearerToken = null,
        Func<string?>? resolveWorkspacePath = null)
    {
        _apiKey = Normalize(apiKey);
        _bearerToken = Normalize(bearerToken);
        _resolveWorkspacePath = resolveWorkspacePath;
    }

    public string? GetApiKey()
    {
        lock (_gate)
            return _apiKey;
    }

    public string? GetBearerToken()
    {
        lock (_gate)
            return _bearerToken;
    }

    public string? GetWorkspacePath()
    {
        lock (_gate)
            return Normalize(_resolveWorkspacePath?.Invoke());
    }

    public void UpdateApiKey(string? apiKey)
    {
        lock (_gate)
            _apiKey = Normalize(apiKey);
    }

    public void UpdateBearerToken(string? bearerToken)
    {
        lock (_gate)
            _bearerToken = Normalize(bearerToken);
    }

    public void UpdateWorkspacePathResolver(Func<string?>? resolveWorkspacePath)
    {
        lock (_gate)
            _resolveWorkspacePath = resolveWorkspacePath;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
