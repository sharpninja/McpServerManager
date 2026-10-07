using FluentAssertions;
using McpServerManager.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.Core.Tests.Services.Infrastructure;

/// <summary>
/// Positive expectations ("a callback fires") wait on a completion signal with a generous
/// ceiling instead of a fixed sleep, so scheduler delay under a loaded parallel test run
/// cannot turn a correct timer into a failure. Negative expectations ("nothing fires after
/// Stop/Dispose") rely on the deterministic Stop/Dispose contract (HV-R4-01).
/// </summary>
public sealed class TimerServiceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);
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

        await firedTwice.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Dispose();

        Volatile.Read(ref count).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateOneShot_ShortDelay_FiresCallbackOnce()
    {
        int count = 0;
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateOneShot(
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                Interlocked.Increment(ref count);
                fired.TrySetResult();
                return Task.CompletedTask;
            });

        await fired.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        handle.Dispose();

        Volatile.Read(ref count).Should().Be(1);
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

        await firedTwice.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = Volatile.Read(ref count);
        await Task.Delay(700, TestContext.Current.CancellationToken);

        Volatile.Read(ref count).Should().Be(snapshot);
    }

    [Fact]
    public async Task Restart_AfterStop_ResumesCallbacks()
    {
        int count = 0;
        int threshold = int.MaxValue;
        var firstFire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                int now = Interlocked.Increment(ref count);
                firstFire.TrySetResult();
                if (now > Volatile.Read(ref threshold))
                {
                    resumed.TrySetResult();
                }

                return Task.CompletedTask;
            });

        await firstFire.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = Volatile.Read(ref count);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Volatile.Read(ref count).Should().Be(snapshot);

        Volatile.Write(ref threshold, snapshot);
        handle.Restart();
        await resumed.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Dispose();

        Volatile.Read(ref count).Should().BeGreaterThan(snapshot);
    }

    [Fact]
    public async Task Dispose_StopsCallbacks()
    {
        int count = 0;
        var firstFire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                Interlocked.Increment(ref count);
                firstFire.TrySetResult();
                return Task.CompletedTask;
            });

        await firstFire.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Dispose();
        int snapshot = Volatile.Read(ref count);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Volatile.Read(ref count).Should().Be(snapshot);
    }

    [Fact]
    public async Task Restart_WithNewInterval_UsesNewInterval()
    {
        int count = 0;
        var firedTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromSeconds(10),
            _ =>
            {
                if (Interlocked.Increment(ref count) >= 2)
                {
                    firedTwice.TrySetResult();
                }

                return Task.CompletedTask;
            });

        // Should not have fired yet with a 10 s interval.
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Volatile.Read(ref count).Should().Be(0);

        // Restart with a much shorter interval; two callbacks prove the new interval is in effect.
        handle.Restart(TimeSpan.FromMilliseconds(50));
        await firedTwice.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Dispose();

        Volatile.Read(ref count).Should().BeGreaterThanOrEqualTo(2);
    }

    // HV-R4-01: once Stop() returns, no callback invocation may still be running its
    // synchronous portion and none may start until Restart(). The callback sleeps after
    // signalling entry so an in-flight invocation always overlaps the Stop() call.
    [Fact]
    public async Task Stop_WhileCallbackInFlight_NoInvocationCompletesAfterStopReturns()
    {
        for (int iteration = 0; iteration < 10; iteration++)
        {
            int completed = 0;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var handle = _sut.CreateRecurring(
                TimeSpan.FromMilliseconds(10),
                _ =>
                {
                    entered.TrySetResult();
                    Thread.Sleep(40);
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

            await entered.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
            handle.Stop();
            int snapshot = Volatile.Read(ref completed);
            await Task.Delay(150, TestContext.Current.CancellationToken);

            Volatile.Read(ref completed).Should().Be(snapshot, $"iteration {iteration} observed a callback after Stop() returned");
        }
    }

    [Fact]
    public async Task Dispose_WhileCallbackInFlight_NoInvocationCompletesAfterDisposeReturns()
    {
        for (int iteration = 0; iteration < 10; iteration++)
        {
            int completed = 0;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handle = _sut.CreateRecurring(
                TimeSpan.FromMilliseconds(10),
                _ =>
                {
                    entered.TrySetResult();
                    Thread.Sleep(40);
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

            await entered.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
            handle.Dispose();
            int snapshot = Volatile.Read(ref completed);
            await Task.Delay(150, TestContext.Current.CancellationToken);

            Volatile.Read(ref completed).Should().Be(snapshot, $"iteration {iteration} observed a callback after Dispose() returned");
        }
    }
}