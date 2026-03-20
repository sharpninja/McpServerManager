using System;
using System.Threading.Tasks;
using McpServer.UI.Core.Services;
using Terminal.Gui;

namespace McpServer.Director;

internal sealed class TuiUiDispatcherService : StrategyUiDispatcherService
{
    public TuiUiDispatcherService()
        : base(new TuiUiDispatchStrategy())
    {
    }
}

internal sealed class TuiUiDispatchStrategy : IUiDispatchStrategy
{
    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Invoke(async () =>
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

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
            return Task.FromResult(action());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Invoke(() =>
        {
            try
            {
                tcs.TrySetResult(action());
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

        if (CheckAccess())
        {
            action();
            return;
        }

        Application.Invoke(action);
    }
}
