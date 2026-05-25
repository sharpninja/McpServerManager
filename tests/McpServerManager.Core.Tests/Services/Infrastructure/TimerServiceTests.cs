using FluentAssertions;
using McpServerManager.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.Core.Tests.Services.Infrastructure;

public sealed class TimerServiceTests
{
    private readonly TimerService _sut = new();

    [Fact]
    public async Task CreateRecurring_ShortInterval_FiresCallbackMultipleTimes()
    {
        int count = 0;
        var firedTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                if (Interlocked.Increment(ref count) >= 2)
                {
                    firedTwice.TrySetResult();
                }

                return Task.CompletedTask;
            });

        await firedTwice.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        handle.Dispose();

        count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateOneShot_ShortDelay_FiresCallbackOnce()
    {
        int count = 0;
        using var handle = _sut.CreateOneShot(
            TimeSpan.FromMilliseconds(50),
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await Task.Delay(300, TestContext.Current.CancellationToken);
        handle.Dispose();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Stop_PreventsSubsequentFiring()
    {
        int count = 0;
        var firedTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(500),
            _ =>
            {
                if (Interlocked.Increment(ref count) >= 2)
                {
                    firedTwice.TrySetResult();
                }

                return Task.CompletedTask;
            });

        await firedTwice.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = count;
        await Task.Delay(700, TestContext.Current.CancellationToken);

        count.Should().Be(snapshot);
    }

    [Fact]
    public async Task Restart_AfterStop_ResumesCallbacks()
    {
        int count = 0;
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await Task.Delay(150, TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = count;
        await Task.Delay(150, TestContext.Current.CancellationToken);
        count.Should().Be(snapshot);

        handle.Restart();
        await Task.Delay(200, TestContext.Current.CancellationToken);
        handle.Dispose();

        count.Should().BeGreaterThan(snapshot);
    }

    [Fact]
    public async Task Dispose_StopsCallbacks()
    {
        int count = 0;
        var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await Task.Delay(150, TestContext.Current.CancellationToken);
        handle.Dispose();
        int snapshot = count;
        await Task.Delay(200, TestContext.Current.CancellationToken);

        count.Should().Be(snapshot);
    }

    [Fact]
    public async Task Restart_WithNewInterval_UsesNewInterval()
    {
        int count = 0;
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(500),
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        // Should not have fired yet with 500ms interval
        await Task.Delay(100, TestContext.Current.CancellationToken);
        count.Should().Be(0);

        // Restart with much shorter interval
        handle.Restart(TimeSpan.FromMilliseconds(50));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        handle.Dispose();

        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
