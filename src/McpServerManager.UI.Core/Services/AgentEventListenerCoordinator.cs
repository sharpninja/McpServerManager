using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.UI.Core.Commands;
using McpServerManager.UI.Core.Models;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Services;

public sealed class AgentEventListenerCoordinatorOptions
{
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan FailureDelay { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed class AgentEventListenerCoordinator(
    IAgentEventStreamReader eventStream,
    IAgentEventStatusTarget statusTarget,
    ISystemNotificationService systemNotification,
    IUiDispatcherService uiDispatcher,
    ILogger<AgentEventListenerCoordinator> logger,
    AgentEventListenerCoordinatorOptions? options = null)
{
    private readonly AgentEventListenerCoordinatorOptions _options = options ?? new AgentEventListenerCoordinatorOptions();
    private readonly object _gate = new();
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _listenerTask is { IsCompleted: false };
        }
    }

    public void Start(bool restart = false)
    {
        if (restart)
            Stop();

        lock (_gate)
        {
            if (_listenerTask is { IsCompleted: false })
                return;

            _listenerCts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => RunListenerLoopAsync(_listenerCts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_gate)
        {
            cts = _listenerCts;
            task = _listenerTask;
            _listenerCts = null;
            _listenerTask = null;
        }

        if (cts is null)
            return;

        cts.Cancel();
        _ = (task ?? Task.CompletedTask).ContinueWith(
            _ => cts.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal async Task ProcessEventAsync(McpIncomingChangeEvent changeEvent, CancellationToken cancellationToken)
    {
        if (!IsActionableAgentEvent(changeEvent))
            return;

        var message = BuildActionableAgentEventMessage(changeEvent);
        await SetStatusAsync(message).ConfigureAwait(false);
        await systemNotification.NotifyAgentEventAsync(changeEvent, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunListenerLoopAsync(CancellationToken cancellationToken)
    {
        var hasReportedFailure = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var changeEvent in eventStream.StreamEventsAsync(cancellationToken).WithCancellation(cancellationToken))
                    await ProcessEventAsync(changeEvent, cancellationToken).ConfigureAwait(false);

                hasReportedFailure = false;

                if (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("[Agent Events] Stream ended; reconnecting.");
                    await DelayAsync(_options.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Agent Events] Listener failed; reconnecting.");
                if (!hasReportedFailure)
                {
                    await SetStatusAsync($"Agent event listener unavailable: {ex.Message}").ConfigureAwait(false);
                    hasReportedFailure = true;
                }

                try
                {
                    await DelayAsync(_options.FailureDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private Task SetStatusAsync(string message)
        => uiDispatcher.InvokeAsync(() =>
        {
            statusTarget.SetAgentEventStatus(message);
            return Task.CompletedTask;
        });

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);

    internal static bool IsActionableAgentEvent(McpIncomingChangeEvent changeEvent)
    {
        if (!IsAgentScopedEvent(changeEvent))
            return false;

        return MatchesActionableAgentState(changeEvent.Action)
            || MatchesActionableAgentState(changeEvent.EventType)
            || MatchesActionableAgentState(changeEvent.Status)
            || MatchesActionableAgentState(TryGetExtensionString(changeEvent, "action"))
            || MatchesActionableAgentState(TryGetExtensionString(changeEvent, "eventType"))
            || MatchesActionableAgentState(TryGetExtensionString(changeEvent, "status"))
            || MatchesActionableAgentState(TryGetExtensionString(changeEvent, "state"));
    }

    private static bool IsAgentScopedEvent(McpIncomingChangeEvent changeEvent)
    {
        if (!string.IsNullOrWhiteSpace(changeEvent.AgentId))
            return true;

        if (!string.IsNullOrWhiteSpace(TryGetExtensionString(changeEvent, "agentId")))
            return true;

        if (!string.IsNullOrWhiteSpace(changeEvent.Category) &&
            changeEvent.Category.Contains("agent", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(changeEvent.ResourceUri) &&
            changeEvent.ResourceUri.Contains("/agent", StringComparison.OrdinalIgnoreCase))
            return true;

        var extensionResourceUri = TryGetExtensionString(changeEvent, "resourceUri");
        return !string.IsNullOrWhiteSpace(extensionResourceUri) &&
               extensionResourceUri.Contains("/agent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesActionableAgentState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        return normalized.Equals("launch", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("launched", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("blocked", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildActionableAgentEventMessage(McpIncomingChangeEvent changeEvent)
    {
        var action = FirstNonEmpty(
                changeEvent.Action,
                changeEvent.EventType,
                changeEvent.Status,
                TryGetExtensionString(changeEvent, "status"),
                TryGetExtensionString(changeEvent, "state"),
                TryGetExtensionString(changeEvent, "action"),
                TryGetExtensionString(changeEvent, "eventType"))
            ?? "updated";

        var normalizedAction = action.Trim().ToLowerInvariant() switch
        {
            "launched" => "launch",
            _ => action.Trim()
        };

        var agentId = FirstNonEmpty(
            changeEvent.AgentId,
            TryGetExtensionString(changeEvent, "agentId"),
            changeEvent.EntityId,
            TryGetExtensionString(changeEvent, "entityId"));

        return string.IsNullOrWhiteSpace(agentId)
            ? $"Agent event: {normalizedAction}"
            : $"Agent {agentId}: {normalizedAction}";
    }

    private static string? TryGetExtensionString(McpIncomingChangeEvent changeEvent, string key)
    {
        if (changeEvent.ExtensionData is null ||
            !changeEvent.ExtensionData.TryGetValue(key, out var extensionValue))
            return null;

        return extensionValue.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => extensionValue.GetString(),
            _ => extensionValue.ToString()
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
