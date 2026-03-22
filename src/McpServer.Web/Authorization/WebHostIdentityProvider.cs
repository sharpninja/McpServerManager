using McpServer.UI.Core.Auth;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.Configuration;

namespace McpServer.Web.Authorization;

internal sealed class WebHostIdentityProvider : IHostIdentityProvider
{
    private readonly BearerTokenAccessor _bearerTokenAccessor;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly IConfiguration _configuration;

    public WebHostIdentityProvider(
        BearerTokenAccessor bearerTokenAccessor,
        WorkspaceContextViewModel workspaceContext,
        IConfiguration configuration)
    {
        _bearerTokenAccessor = bearerTokenAccessor;
        _workspaceContext = workspaceContext;
        _configuration = configuration;
    }

    public string? GetBearerToken()
        => Normalize(_bearerTokenAccessor.GetAccessTokenAsync().GetAwaiter().GetResult());

    public string? GetApiKey()
        => Normalize(_configuration["McpServer:ApiKey"]);

    public string? GetWorkspacePath()
        => Normalize(_workspaceContext.ActiveWorkspacePath)
           ?? Normalize(_configuration["McpServer:WorkspacePath"]);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
