using System.Net.Sockets;

namespace McpServer.Web;

internal static class StartupFailureDiagnostics
{
    public static string? BuildOperatorHint(Exception exception, string? effectiveListenUrls = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!IsAddressAlreadyInUse(exception))
        {
            return null;
        }

        var endpointDescription = TryExtractBoundAddress(exception)
            ?? effectiveListenUrls
            ?? "the configured HTTP endpoint";

        return $"Startup failed because another process is already listening on {endpointDescription}. Launch this process with --urls or ASPNETCORE_URLS to force a fixed address, or let mcp-web choose the next available loopback port starting at {WebListenUrlSelector.DefaultStartPort}.";
    }

    internal static bool IsAddressAlreadyInUse(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Enumerate(exception).Any(static current =>
            current is SocketException socketException && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse
            || string.Equals(current.GetType().Name, "AddressInUseException", StringComparison.Ordinal));
    }

    private static IEnumerable<Exception> Enumerate(Exception root)
    {
        var pending = new Queue<Exception>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            yield return current;

            if (current is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    pending.Enqueue(inner);
                }

                continue;
            }

            if (current.InnerException is not null)
            {
                pending.Enqueue(current.InnerException);
            }
        }
    }

    private static string? TryExtractBoundAddress(Exception exception)
    {
        foreach (var current in Enumerate(exception))
        {
            var message = current.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            const string prefix = "Failed to bind to address ";
            const string suffix = ": address already in use";

            var prefixIndex = message.IndexOf(prefix, StringComparison.Ordinal);
            var suffixIndex = message.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0 || suffixIndex <= prefixIndex)
            {
                continue;
            }

            var addressStart = prefixIndex + prefix.Length;
            var addressLength = suffixIndex - addressStart;
            if (addressLength <= 0)
            {
                continue;
            }

            return message.Substring(addressStart, addressLength).Trim();
        }

        return null;
    }
}
