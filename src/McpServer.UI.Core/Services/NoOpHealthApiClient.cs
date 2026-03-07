using McpServer.UI.Core.Messages;

namespace McpServer.UI.Core.Services;

/// <summary>
/// Default no-op health client used when a host does not provide an implementation.
/// </summary>
public sealed class NoOpHealthApiClient : IHealthApiClient
{
    /// <inheritdoc />
    public Task<HealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new HealthSnapshot(
            DateTimeOffset.UtcNow,
            "healthy",
            "{\"status\":\"healthy\",\"source\":\"noop\"}",
            null));
}
