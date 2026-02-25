using System;
using Android.Content;

namespace McpServerManager.Android.Services;

/// <summary>Persists the Android connect-dialog host/port for reuse on next startup.</summary>
internal static class AndroidConnectionPreferencesService
{
    private const string PreferencesName = "McpServerManager.Connection";
    private const string HostKey = "Host";
    private const string PortKey = "Port";

    public static bool TryLoad(out string host, out string port)
    {
        host = string.Empty;
        port = string.Empty;

        try
        {
            var prefs = GetPreferences();
            var storedHost = prefs?.GetString(HostKey, null)?.Trim();
            var storedPort = prefs?.GetString(PortKey, null)?.Trim();

            if (string.IsNullOrWhiteSpace(storedHost) || string.IsNullOrWhiteSpace(storedPort))
                return false;

            host = storedHost;
            port = storedPort;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(string host, string port)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
            return;

        var prefs = GetPreferences();
        if (prefs == null)
            return;

        var editor = prefs.Edit();
        if (editor == null)
            return;

        using (editor)
        {
            editor.PutString(HostKey, host.Trim());
            editor.PutString(PortKey, port.Trim());
            editor.Apply();
        }
    }

    private static ISharedPreferences? GetPreferences()
    {
        var context = global::Android.App.Application.Context;
        return context?.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }
}
