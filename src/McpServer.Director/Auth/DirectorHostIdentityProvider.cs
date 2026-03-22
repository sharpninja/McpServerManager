using McpServer.UI.Core.Auth;

namespace McpServer.Director.Auth;

internal sealed class DirectorHostIdentityProvider : IHostIdentityProvider
{
    private readonly DirectorMcpContext _context;

    public DirectorHostIdentityProvider(DirectorMcpContext context)
    {
        _context = context;
    }

    public string? GetBearerToken()
    {
        _ = McpHttpClient.TryRefreshCachedToken();
        var cached = TokenCache.Load();
        if (cached is null || cached.IsExpired || string.IsNullOrWhiteSpace(cached.AccessToken))
            return null;

        return cached.AccessToken;
    }

    public string? GetApiKey()
        => Normalize(_context.ActiveWorkspaceClient?.ApiKey)
           ?? Normalize(_context.ControlClient?.ApiKey);

    public string? GetWorkspacePath()
        => Normalize(_context.ActiveWorkspacePath);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
