using System.Collections.Generic;
using System.Threading;
using McpServerManager.UI.Core.Models;

namespace McpServerManager.UI.Core.Services;

public interface IAgentEventStreamReader
{
    IAsyncEnumerable<McpIncomingChangeEvent> StreamEventsAsync(CancellationToken cancellationToken = default);
}

internal sealed class McpAgentEventStreamReader(McpAgentEventStreamService service) : IAgentEventStreamReader
{
    public IAsyncEnumerable<McpIncomingChangeEvent> StreamEventsAsync(CancellationToken cancellationToken = default)
        => service.StreamEventsAsync(cancellationToken: cancellationToken);
}

internal sealed class NoOpAgentEventStreamReader : IAgentEventStreamReader
{
    public async IAsyncEnumerable<McpIncomingChangeEvent> StreamEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield break;
    }
}
