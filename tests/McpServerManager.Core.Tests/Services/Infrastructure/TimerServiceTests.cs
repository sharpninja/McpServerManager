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
            TimeSpan.FromHours(1),
            _ =>
            {
                if (Interlocked.Increment(ref count) >= 2)
                {
                    firedTwice.TrySetResult();
                }

                return Task.CompletedTask;
            });

        // Should not have fired yet with a 1 h interval.
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Volatile.Read(ref count).Should().Be(0);

        // Restart with a much shorter interval. Two callbacks inside the 30 s ceiling can only come from the
        // new 50 ms interval; a Restart that ignored it would keep the 1 h interval and time out (HV-R5-01).
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

    // HV-R5-02: a synchronous, cancellation-aware callback must be cancelled by Dispose rather than
    // waited on; otherwise Dispose and the callback wait on each other forever.
    [Fact]
    public async Task Dispose_WhileCancellationAwareCallbackBlocks_CancelsTokenAndReturns()
    {
        using var entered = new ManualResetEventSlim();
        using var rescue = new ManualResetEventSlim();
        CancellationToken observed = default;
        var handle = _sut.CreateOneShot(
            TimeSpan.FromMilliseconds(1),
            ct =>
            {
                observed = ct;
                entered.Set();
                WaitHandle.WaitAny([ct.WaitHandle, rescue.WaitHandle]);
                return Task.CompletedTask;
            });

        try
        {
            entered.Wait(SignalTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
            var dispose = Task.Factory.StartNew(handle.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            bool returned = await Task.WhenAny(dispose, Task.Delay(SignalTimeout, TestContext.Current.CancellationToken)) == dispose;

            returned.Should().BeTrue("Dispose must cancel the in-flight callback instead of waiting on it");
            observed.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            rescue.Set();
        }
    }

    [Fact]
    public async Task Stop_WhileCancellationAwareCallbackBlocks_CancelsTokenAndReturns()
    {
        using var entered = new ManualResetEventSlim();
        using var rescue = new ManualResetEventSlim();
        CancellationToken observed = default;
        using var handle = _sut.CreateOneShot(
            TimeSpan.FromMilliseconds(1),
            ct =>
            {
                observed = ct;
                entered.Set();
                WaitHandle.WaitAny([ct.WaitHandle, rescue.WaitHandle]);
                return Task.CompletedTask;
            });

        try
        {
            entered.Wait(SignalTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
            var stop = Task.Factory.StartNew(handle.Stop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            bool returned = await Task.WhenAny(stop, Task.Delay(SignalTimeout, TestContext.Current.CancellationToken)) == stop;

            returned.Should().BeTrue("Stop must cancel the in-flight callback instead of waiting on it");
            observed.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            rescue.Set();
        }
    }

    [Fact]
    public async Task Restart_AfterStop_SuppliesUncancelledToken()
    {
        int restarted = 0;
        var firstFire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumedTokenCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            ct =>
            {
                if (Volatile.Read(ref restarted) == 1)
                {
                    resumedTokenCancelled.TrySetResult(ct.IsCancellationRequested);
                }
                else
                {
                    firstFire.TrySetResult();
                }

                return Task.CompletedTask;
            });

        await firstFire.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Stop();
        Volatile.Write(ref restarted, 1);
        handle.Restart();

        (await resumedTokenCancelled.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    // Dispose cancels the token handed to an in-flight asynchronous invocation, but the
    // invocation's continuation may still observe that token after Dispose returns. The token
    // must stay usable (cancelled, not disposed), matching how Restart treats a stopped run.
    [Fact]
    public async Task Dispose_WhileAsyncCallbackPending_TokenStaysObservableAfterDisposeReturns()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcome = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cancelledAfterDispose = false;
        var handle = _sut.CreateOneShot(
            TimeSpan.FromMilliseconds(1),
            async ct =>
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
                try
                {
                    cancelledAfterDispose = ct.WaitHandle.WaitOne(0);
                    using (ct.Register(static () => { }))
                    {
                    }

                    outcome.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    outcome.TrySetResult(ex);
                }
            });

        await entered.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
        handle.Dispose();
        release.TrySetResult();
        var error = await outcome.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);

        error.Should().BeNull("the token supplied to an in-flight invocation must not be disposed under it");
        cancelledAfterDispose.Should().BeTrue();
    }
}
