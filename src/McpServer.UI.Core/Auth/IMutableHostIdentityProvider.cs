using System;

namespace McpServerManager.UI.Core.Auth;

internal interface IMutableHostIdentityProvider : IHostIdentityProvider
{
    void UpdateApiKey(string? apiKey);

    void UpdateBearerToken(string? bearerToken);

    void UpdateWorkspacePathResolver(Func<string?>? resolveWorkspacePath);
}
