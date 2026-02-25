using System;
using System.Linq;

namespace McpServerManager.Android.Services;

internal static class AndroidBackNavigationService
{
    public static event Func<bool>? BackRequested;

    public static bool TryHandleBack()
    {
        var handlers = BackRequested;
        if (handlers == null)
            return false;

        foreach (var handler in handlers.GetInvocationList().OfType<Func<bool>>().Reverse())
        {
            try
            {
                if (handler())
                    return true;
            }
            catch
            {
                // Let Android proceed with normal back behavior if a handler fails.
            }
        }

        return false;
    }
}
