namespace McpServerManager.UI.Core.Auth;

public interface IHostIdentityProvider
{
    string? GetBearerToken();

    string? GetApiKey();

    string? GetWorkspacePath();
}
