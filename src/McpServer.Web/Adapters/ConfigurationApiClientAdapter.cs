using McpServer.UI.Core.Services;

namespace McpServer.Web.Adapters;

internal sealed class ConfigurationApiClientAdapter : IConfigurationApiClient
{
    private readonly WebMcpContext _context;

    public ConfigurationApiClientAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.UseControlApiClientAsync(
                static (client, ct) => client.Configuration.GetValuesAsync(ct),
                cancellationToken)
            .ConfigureAwait(true);
    }

    public async Task<IReadOnlyDictionary<string, string>> PatchValuesAsync(
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        return await _context.UseControlApiClientAsync(
                (client, ct) => client.Configuration.PatchValuesAsync(values, ct),
                cancellationToken)
            .ConfigureAwait(true);
    }
}
