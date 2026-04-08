using System;
using System.Threading.Tasks;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Default dispatcher that executes work inline when no UI dispatcher is supplied by host.
/// </summary>
public sealed class ImmediateUiDispatcherService : StrategyUiDispatcherService
{
    /// <summary>
    /// Initializes the immediate dispatcher service.
    /// </summary>
    public ImmediateUiDispatcherService()
        : base(new ImmediateUiDispatchStrategy())
    {
    }
}

internal sealed class ImmediateUiDispatchStrategy : IUiDispatchStrategy
{
    public bool CheckAccess() => true;

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action();
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Task.FromResult(action());
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
