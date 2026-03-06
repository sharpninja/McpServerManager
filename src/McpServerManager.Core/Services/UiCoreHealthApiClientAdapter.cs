using McpServer.Client;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace McpServerManager.Core.Services;

internal sealed class UiCoreHealthApiClientAdapter : IHealthApiClient
{
    private readonly McpServerClient _client;
    private readonly string? _serverBaseUrl;

    public UiCoreHealthApiClientAdapter(McpServerClient client, Uri? baseUri)
    {
        _client = client;
        _serverBaseUrl = baseUri?.ToString().TrimEnd('/');
    }

    public async Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.Health.GetAsync(cancellationToken);
        var raw = JsonSerializer.Serialize(result);
        return new HealthSnapshot(
            CheckedAt: DateTimeOffset.UtcNow,
            Status: result.Status ?? "Unknown",
            RawPayload: raw,
            ServerBaseUrl: _serverBaseUrl
        );
    }
}
