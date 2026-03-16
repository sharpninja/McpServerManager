using System;
using System.Threading.Tasks;

namespace McpServer.UI.Core.Services;

/// <summary>
/// Host UI dispatcher abstraction for marshaling state updates to UI thread/context.
/// </summary>
public interface IUiDispatcherService
{
    /// <summary>Returns <see langword="true"/> when the caller is already on the UI context.</summary>
    bool CheckAccess();

    /// <summary>Invokes an asynchronous action on the UI context.</summary>
    Task InvokeAsync(Func<Task> action);

    /// <summary>Posts an action for execution on the UI context.</summary>
    void Post(Action action);
}
