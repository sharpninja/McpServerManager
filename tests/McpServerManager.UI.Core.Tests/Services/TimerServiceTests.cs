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

            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
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
        using var handle = _sut.CreateRecurring(
            TimeSpan.FromMilliseconds(50),
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await Task.Delay(150, TestContext.Current.CancellationToken);
        handle.Stop();
        int snapshot = Volatile.Read(ref count);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot, Volatile.Read(ref count));

        handle.Restart();
        await Task.Delay(250, TestContext.Current.CancellationToken);
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

            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            handle.Dispose();
            int snapshot = Volatile.Read(ref completed);
            await Task.Delay(150, TestContext.Current.CancellationToken);

            Assert.Equal(snapshot, Volatile.Read(ref completed));
        }
    }
}