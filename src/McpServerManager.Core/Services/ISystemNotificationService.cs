using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.UI.Core.Models;

namespace McpServerManager.Core.Services;

/// <summary>
/// Abstraction for delivering system-level notifications from actionable agent events.
/// </summary>
public interface ISystemNotificationService : McpServer.UI.Core.Services.ISystemNotificationService
{
    /// <summary>
    /// Notifies the user about an actionable agent event.
    /// </summary>
    new Task NotifyAgentEventAsync(
        McpIncomingChangeEvent changeEvent,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default no-op notification service when a platform implementation is not provided.
/// </summary>
public sealed class NoOpSystemNotificationService : ISystemNotificationService
{
    public static NoOpSystemNotificationService Instance { get; } = new();

    private NoOpSystemNotificationService()
    {
    }

    public Task NotifyAgentEventAsync(
        McpIncomingChangeEvent changeEvent,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeEvent);
        return Task.CompletedTask;
    }
}
