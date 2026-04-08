using System;
using System.Threading.Tasks;
using System.Windows;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.VsExtension.McpTodo;

internal sealed class VsixUiDispatcherService : StrategyUiDispatcherService
{
    public VsixUiDispatcherService()
        : base(new WpfUiDispatchStrategy())
    {
    }
}

internal sealed class WpfUiDispatchStrategy : IUiDispatchStrategy
{
    private static System.Windows.Threading.Dispatcher CurrentDispatcher
        => Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

    public bool CheckAccess() => CurrentDispatcher.CheckAccess();

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CurrentDispatcher.CheckAccess())
            return action();

        return CurrentDispatcher.InvokeAsync(action).Task.Unwrap();
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CurrentDispatcher.CheckAccess())
            return Task.FromResult(action());

        return CurrentDispatcher.InvokeAsync(action).Task;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CurrentDispatcher.CheckAccess())
        {
            action();
            return;
        }

        CurrentDispatcher.BeginInvoke(action);
    }
}
