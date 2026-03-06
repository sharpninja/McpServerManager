using System;
using Avalonia.Threading;

namespace McpServerManager.Core.Services;

/// <summary>
/// UI dispatcher adapter that marshals callbacks to Avalonia UI thread.
/// </summary>
public sealed class AvaloniaUiDispatcherService : McpServer.UI.Core.Services.IUiDispatcherService
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
