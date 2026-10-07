using McpServerManager.UI.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

/// <summary>
/// HV-R4-01 parity: the UI.Core host timer must honour the same Stop/Dispose contract as
/// the Core implementation. Once Stop() or Dispose() returns, no callback invocation may
/// still be running its synchronous portion and none may start until Restart().
/// </summary>
public sealed class TimerServiceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);
    private readonly TimerService _sut = new();

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

            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            handle.Stop();
            int snapshot = Volatile.Read(ref completed);
            await Task.Delay(150, TestContext.Current.CancellationToken);

            Assert.Equal(snapshot, Volatile.Read(ref completed));
        }
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

        await firstFire.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = Volatile.Read(ref count);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot, Volatile.Read(ref count));

        Volatile.Write(ref threshold, snapshot);
        handle.Restart();
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        handle.Dispose();

        Assert.True(Volatile.Read(ref count) > snapshot);
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

            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            handle.Dispose();
            int snapshot = Volatile.Read(ref completed);
            await Task.Delay(150, TestContext.Current.CancellationToken);

            Assert.Equal(snapshot, Volatile.Read(ref completed));
        }
    }

    // HV-R5-01 parity: a Restart that ignored the new interval would keep the 1 h interval and time out.
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

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref count));

        handle.Restart(TimeSpan.FromMilliseconds(50));
        await firedTwice.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);

        Assert.True(Volatile.Read(ref count) >= 2);
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
            Assert.True(entered.Wait(SignalTimeout, TestContext.Current.CancellationToken));
            var dispose = Task.Factory.StartNew(handle.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            bool returned = await Task.WhenAny(dispose, Task.Delay(SignalTimeout, TestContext.Current.CancellationToken)) == dispose;

            Assert.True(returned);
            Assert.True(observed.IsCancellationRequested);
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
            Assert.True(entered.Wait(SignalTimeout, TestContext.Current.CancellationToken));
            var stop = Task.Factory.StartNew(handle.Stop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            bool returned = await Task.WhenAny(stop, Task.Delay(SignalTimeout, TestContext.Current.CancellationToken)) == stop;

            Assert.True(returned);
            Assert.True(observed.IsCancellationRequested);
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

        Assert.False(await resumedTokenCancelled.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));
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

        Assert.Null(error);
        Assert.True(cancelledAfterDispose);
    }
}
