using System.Text.Json;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;

namespace McpServer.Web.Adapters;

internal sealed class HealthApiClientAdapter : IHealthApiClient
{
    private readonly WebMcpContext _context;

    public HealthApiClientAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public async Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = await _context.UseControlApiClientAsync(
                static (client, ct) => client.Health.GetAsync(ct),
                cancellationToken)
            .ConfigureAwait(true);
        var raw = JsonSerializer.Serialize(health);
        var status = string.IsNullOrWhiteSpace(health.Status) ? "unknown" : health.Status;

        return new HealthSnapshot(
            CheckedAt: DateTimeOffset.UtcNow,
            Status: status,
            RawPayload: raw,
            ServerBaseUrl: _context.BaseUrl.ToString());
    }
}
