using McpServer.Client;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;

namespace McpServer.Web.Adapters;

internal sealed class EventStreamApiClientAdapter : IEventStreamApiClient
{
    private readonly WebMcpContext _context;

    public EventStreamApiClientAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public async Task<IAsyncEnumerable<ChangeEventItem>> SubscribeAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(true);
        var stream = client.Events.SubscribeAsync(category, cancellationToken);
        return MapAsync(stream);
    }

    private async Task<McpServerClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_context.ActiveWorkspacePath))
            return await _context.GetRequiredControlApiClientAsync(cancellationToken).ConfigureAwait(true);

        return await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async IAsyncEnumerable<ChangeEventItem> MapAsync(IAsyncEnumerable<McpServer.Client.Models.ChangeEvent> stream)
    {
        await foreach (var item in stream.ConfigureAwait(true))
        {
            yield return new ChangeEventItem(
                item.Category,
                item.Action,
                item.EntityId,
                item.ResourceUri,
                item.Timestamp);
        }
    }
}
