using System;
using System.Threading.Tasks;

namespace McpServer.UI.Core.Services;

/// <summary>
/// Strategy for marshaling work onto a host UI thread or synchronization context.
/// </summary>
public interface IUiDispatchStrategy
{
    /// <summary>Returns <see langword="true"/> when the caller already has UI access.</summary>
    bool CheckAccess();

    /// <summary>Invokes an asynchronous action on the UI context.</summary>
    Task InvokeAsync(Func<Task> action);

    /// <summary>Posts a synchronous action to the UI context.</summary>
    void Post(Action action);
}
