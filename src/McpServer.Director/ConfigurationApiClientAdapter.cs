using McpServerManager.UI.Core.Services;

namespace McpServerManager.Director;

/// <summary>Director adapter for <see cref="IConfigurationApiClient"/>.</summary>
internal sealed class ConfigurationApiClientAdapter : IConfigurationApiClient
{
    private readonly DirectorMcpContext _context;

    public ConfigurationApiClientAdapter(DirectorMcpContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await _context.GetRequiredControlApiClientAsync(cancellationToken).ConfigureAwait(true);
        var result = await client.Configuration.GetValuesAsync(cancellationToken).ConfigureAwait(true);
        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> PatchValuesAsync(
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var client = await _context.GetRequiredControlApiClientAsync(cancellationToken).ConfigureAwait(true);
        var result = await client.Configuration.PatchValuesAsync(values, cancellationToken).ConfigureAwait(true);
        return result;
    }
}
