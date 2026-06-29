namespace McpServerManager.UI.Core.Auth;

/// <summary>
/// Resolves workspace paths for shared auth token cache operations.
/// </summary>
public static class WorkspaceAuthTokenCachePaths
{
    /// <summary>
    /// Resolves an explicit workspace path or, when omitted, the current directory if it looks like an MCP workspace.
    /// </summary>
    public static string? ResolveWorkspacePath(string? workspacePath = null)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath))
            return Path.GetFullPath(workspacePath.Trim());

        var currentDirectory = Directory.GetCurrentDirectory();
        if (LooksLikeWorkspace(currentDirectory))
            return Path.GetFullPath(currentDirectory);

        return null;
    }

    private static bool LooksLikeWorkspace(string directory)
        => File.Exists(Path.Combine(directory, "AGENTS-README-FIRST.yaml")) ||
           Directory.Exists(Path.Combine(directory, ".mcpServer"));
}
