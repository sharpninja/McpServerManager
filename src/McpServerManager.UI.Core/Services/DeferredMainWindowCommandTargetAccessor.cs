using System;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Services;

public sealed class DeferredMainWindowCommandTargetAccessor
{
    private readonly object _sync = new();
    private MainWindowViewModel? _target;

    public void Attach(MainWindowViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (_sync)
        {
            _target = target;
        }
    }

    public Commands.ICommandTarget RequireTarget()
    {
        lock (_sync)
        {
            return _target ?? throw new InvalidOperationException("The main-window command target has not been attached yet.");
        }
    }
}
