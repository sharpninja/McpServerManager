using System;
using System.Threading;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

internal static class AndroidOidcJwtCacheInvalidationMonitor
{
    private const string UnauthorizedExceptionMarker = "McpServer.Client.McpUnauthorizedException";
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        AppLogService.Instance.EntryAdded += OnEntryAdded;
        global::Android.Util.Log.Info("McpSM", "[AndroidOidcJwtCacheInvalidationMonitor] initialized");
    }

    private static void OnEntryAdded(LogEntry entry)
    {
        try
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Message))
                return;

            // /mcpserver/* client calls surface unauthorized responses as McpUnauthorizedException.
            // This intentionally does not trigger for the raw HttpClient /api-key probe path.
            if (entry.Message.IndexOf(UnauthorizedExceptionMarker, StringComparison.Ordinal) < 0)
                return;

            AndroidConnectionPreferencesService.ClearOidcJwt();
            global::Android.Util.Log.Warn("McpSM", "[AndroidOidcJwtCacheInvalidationMonitor] Cleared cached OIDC JWT after /mcp unauthorized response");
        }
        catch
        {
            // Never let diagnostics/auth-cache cleanup crash the app.
        }
    }
}
