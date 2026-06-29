using System.Text.Json;

namespace McpServerManager.UI.Core.Auth;

/// <summary>
/// Stores workspace identity tokens under <c>.mcpServer/auth/tokens.json</c>.
/// </summary>
public sealed class FileWorkspaceAuthTokenCache : IWorkspaceAuthTokenCache
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a file-backed workspace token cache.</summary>
    public FileWorkspaceAuthTokenCache()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    internal FileWorkspaceAuthTokenCache(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow;
    }

    /// <inheritdoc />
    public WorkspaceAuthToken? TryReadValid(string? workspacePath)
    {
        var cachePath = TryGetCachePath(workspacePath);
        if (cachePath is null || !File.Exists(cachePath))
            return null;

        try
        {
            var json = File.ReadAllText(cachePath);
            var token = JsonSerializer.Deserialize<WorkspaceAuthToken>(json, s_jsonOptions);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                return null;

            if (!token.IsExpired(_utcNow()))
                return token;
        }
        catch
        {
            return null;
        }

        Clear(workspacePath);
        return null;
    }

    /// <inheritdoc />
    public void Save(string? workspacePath, WorkspaceAuthToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            return;

        var cachePath = TryGetCachePath(workspacePath);
        if (cachePath is null)
            return;

        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(token, s_jsonOptions);
        File.WriteAllText(cachePath, json);
    }

    /// <inheritdoc />
    public void Clear(string? workspacePath)
    {
        var cachePath = TryGetCachePath(workspacePath);
        if (cachePath is not null && File.Exists(cachePath))
            File.Delete(cachePath);
    }

    /// <inheritdoc />
    public string? TryGetCachePath(string? workspacePath)
    {
        var resolved = WorkspaceAuthTokenCachePaths.ResolveWorkspacePath(workspacePath);
        return resolved is null ? null : GetCachePath(resolved);
    }

    /// <summary>Returns the cache path for a known workspace path.</summary>
    public string GetCachePath(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        return Path.Combine(workspacePath, ".mcpServer", "auth", "tokens.json");
    }
}
