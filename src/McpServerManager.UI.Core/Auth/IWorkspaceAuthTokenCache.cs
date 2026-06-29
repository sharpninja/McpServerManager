namespace McpServerManager.UI.Core.Auth;

/// <summary>
/// Reads and writes identity-server tokens scoped to a workspace.
/// </summary>
public interface IWorkspaceAuthTokenCache
{
    /// <summary>
    /// Loads a valid cached token for the workspace. Expired cached tokens are deleted and reported as missing.
    /// </summary>
    WorkspaceAuthToken? TryReadValid(string? workspacePath);

    /// <summary>Saves a token for the workspace. No file is written when no workspace is active.</summary>
    void Save(string? workspacePath, WorkspaceAuthToken token);

    /// <summary>Deletes the cached token for the workspace, if present.</summary>
    void Clear(string? workspacePath);

    /// <summary>Gets the workspace token cache file path, or <see langword="null"/> when no workspace is active.</summary>
    string? TryGetCachePath(string? workspacePath);
}
