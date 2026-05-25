using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Adapters;

internal sealed class AuthConfigApiClientAdapter : IAuthConfigApiClient
{
    private readonly WebMcpContext _context;

    public AuthConfigApiClientAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public async Task<AuthConfigSnapshot> GetAuthConfigAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.UseControlApiClientAsync(
                static (client, ct) => client.AuthConfig.GetConfigAsync(ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new AuthConfigSnapshot(
            result.Enabled,
            result.Authority,
            result.ClientId,
            result.Scopes,
            result.DeviceAuthorizationEndpoint,
            result.TokenEndpoint,
            DateTimeOffset.UtcNow);
    }
}
