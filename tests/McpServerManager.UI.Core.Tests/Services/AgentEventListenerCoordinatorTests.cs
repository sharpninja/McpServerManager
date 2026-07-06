using System.Runtime.CompilerServices;
using System.Text.Json;
using McpServerManager.UI.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

public sealed class AgentEventListenerCoordinatorTests
{
    [Fact]
    public async Task ProcessEventAsync_ActionableAgentEvent_UpdatesStatusAndNotifies()
    {
        var reader = new EmptyAgentEventStreamReader();
        var status = Substitute.For<IAgentEventStatusTarget>();
        var notifications = Substitute.For<ISystemNotificationService>();
        var coordinator = CreateCoordinator(reader, status, notifications);
        var changeEvent = new McpIncomingChangeEvent
        {
            Category = "agent",
            Status = "completed",
            AgentId = "agent-42"
        };

        await coordinator.ProcessEventAsync(changeEvent, CancellationToken.None);

        status.Received(1).SetAgentEventStatus("Agent agent-42: completed");
        await notifications.Received(1).NotifyAgentEventAsync(
            changeEvent,
            "Agent agent-42: completed",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_NonActionableAgentEvent_DoesNotUpdateStatusOrNotify()
    {
        var reader = new EmptyAgentEventStreamReader();
        var status = Substitute.For<IAgentEventStatusTarget>();
        var notifications = Substitute.For<ISystemNotificationService>();
        var coordinator = CreateCoordinator(reader, status, notifications);
        var changeEvent = new McpIncomingChangeEvent
        {
            Category = "workspace",
            Status = "updated"
        };

        await coordinator.ProcessEventAsync(changeEvent, CancellationToken.None);

        status.DidNotReceive().SetAgentEventStatus(Arg.Any<string>());
        await notifications.DidNotReceive().NotifyAgentEventAsync(
            Arg.Any<McpIncomingChangeEvent>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildActionableAgentEventMessage_UsesExtensionDataFallbacks()
    {
        var changeEvent = new McpIncomingChangeEvent
        {
            Category = "agent",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["agentId"] = JsonDocument.Parse("\"agent-ext\"").RootElement.Clone(),
                ["state"] = JsonDocument.Parse("\"failed\"").RootElement.Clone()
            }
        };

        Assert.True(AgentEventListenerCoordinator.IsActionableAgentEvent(changeEvent));
        Assert.Equal("Agent agent-ext: failed", AgentEventListenerCoordinator.BuildActionableAgentEventMessage(changeEvent));
    }

    [Fact]
    public async Task Start_ReconnectsWhenStreamCompletes()
    {
        var reader = new ReconnectingAgentEventStreamReader();
        var status = Substitute.For<IAgentEventStatusTarget>();
        var notifications = Substitute.For<ISystemNotificationService>();
        var coordinator = CreateCoordinator(reader, status, notifications);

        coordinator.Start();
        await reader.SecondSubscription.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Stop();

        Assert.True(reader.StreamCalls >= 2);
    }

    private static AgentEventListenerCoordinator CreateCoordinator(
        IAgentEventStreamReader reader,
        IAgentEventStatusTarget status,
        ISystemNotificationService notifications)
        => new(
            reader,
            status,
            notifications,
            new ImmediateUiDispatcherService(),
            NullLogger<AgentEventListenerCoordinator>.Instance,
            new AgentEventListenerCoordinatorOptions
            {
                ReconnectDelay = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero
            });

    private sealed class EmptyAgentEventStreamReader : IAgentEventStreamReader
    {
        public async IAsyncEnumerable<McpIncomingChangeEvent> StreamEventsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class ReconnectingAgentEventStreamReader : IAgentEventStreamReader
    {
        private int _streamCalls;

        public int StreamCalls => Volatile.Read(ref _streamCalls);

        public TaskCompletionSource SecondSubscription { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<McpIncomingChangeEvent> StreamEventsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _streamCalls);
            if (call == 2)
                SecondSubscription.TrySetResult();

            if (call >= 2)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                yield break;
            }

            await Task.Yield();
            yield break;
        }
    }
}
