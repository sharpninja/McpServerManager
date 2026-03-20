using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpServer.UI.Core.Services;

/// <summary>
/// Process-wide access point for the active host UI dispatcher strategy.
/// </summary>
public static class UiDispatcherHost
{
    private static IUiDispatcherService _current = new ImmediateUiDispatcherService();

    /// <summary>Gets the active host UI dispatcher service.</summary>
    public static IUiDispatcherService Current => Volatile.Read(ref _current);

    /// <summary>Configures the active host UI dispatcher service.</summary>
    public static void Configure(IUiDispatcherService dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        Interlocked.Exchange(ref _current, dispatcher);
    }

    /// <summary>Returns <see langword="true"/> when the caller already has UI access.</summary>
    public static bool CheckAccess() => Current.CheckAccess();

    /// <summary>Posts an action to the active UI dispatcher.</summary>
    public static void Post(Action action) => Current.Post(action);

    /// <summary>Invokes an asynchronous action on the active UI dispatcher.</summary>
    public static Task InvokeAsync(Func<Task> action) => Current.InvokeAsync(action);

    /// <summary>Invokes a synchronous function on the active UI dispatcher and returns its result.</summary>
    public static Task<T> InvokeAsync<T>(Func<T> action) => Current.InvokeAsync(action);
}
