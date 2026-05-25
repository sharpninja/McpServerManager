using System;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Services;

internal sealed class BlazorUiDispatcherService : StrategyUiDispatcherService
{
    public BlazorUiDispatcherService()
        : base(new BlazorUiDispatchStrategy())
    {
    }
}

internal sealed class BlazorUiDispatchStrategy : IUiDispatchStrategy
{
    private SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;

    public bool CheckAccess()
    {
        var context = GetOrCaptureSynchronizationContext();
        return context is null || ReferenceEquals(SynchronizationContext.Current, context);
    }

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = GetOrCaptureSynchronizationContext();
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(async _ =>
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
        }, null);

        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = GetOrCaptureSynchronizationContext();
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
            return Task.FromResult(action());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(_ =>
        {
            try
            {
                tcs.TrySetResult(action());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = GetOrCaptureSynchronizationContext();
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            action();
            return;
        }

        context.Post(_ => action(), null);
    }

    private SynchronizationContext? GetOrCaptureSynchronizationContext()
    {
        var current = SynchronizationContext.Current;
        if (current is not null)
            Interlocked.CompareExchange(ref _synchronizationContext, current, null);

        return _synchronizationContext;
    }
}
