using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using McpServer.UI.Core.Services;

namespace McpServerManager.Core.Services;

/// <summary>
/// UI dispatcher adapter that marshals callbacks to Avalonia UI thread.
/// </summary>
public sealed class AvaloniaUiDispatcherService : StrategyUiDispatcherService
{
    /// <summary>
    /// Initializes the Avalonia dispatcher service.
    /// </summary>
    public AvaloniaUiDispatcherService()
        : base(new AvaloniaUiDispatchStrategy())
    {
    }
}

internal sealed class AvaloniaUiDispatchStrategy : IUiDispatchStrategy
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await action().ConfigureAwait(true);
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
