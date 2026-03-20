using System;
using System.Threading.Tasks;

namespace McpServer.UI.Core.Services;

/// <summary>
/// Reusable <see cref="IUiDispatcherService"/> implementation that delegates UI marshaling to a strategy.
/// </summary>
public class StrategyUiDispatcherService : IUiDispatcherService
{
    private readonly IUiDispatchStrategy _strategy;

    /// <summary>
    /// Initializes a strategy-backed UI dispatcher service.
    /// </summary>
    /// <param name="strategy">Framework-specific UI dispatch strategy.</param>
    public StrategyUiDispatcherService(IUiDispatchStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategy = strategy;
    }

    /// <summary>
    /// Gets the active UI dispatch strategy.
    /// </summary>
    protected IUiDispatchStrategy Strategy => _strategy;

    /// <inheritdoc />
    public bool CheckAccess() => _strategy.CheckAccess();

    /// <inheritdoc />
    public Task InvokeAsync(Func<Task> action) => _strategy.InvokeAsync(action);

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<T> action) => _strategy.InvokeAsync(action);

    /// <inheritdoc />
    public void Post(Action action) => _strategy.Post(action);
}
