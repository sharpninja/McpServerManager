using System;
using Android.Content;

namespace McpServerManager.Android.Services;

/// <summary>Persists the Android connect-dialog host/port for reuse on next startup.</summary>
internal static class AndroidConnectionPreferencesService
{
    private const string PreferencesName = "McpServerManager.Connection";
    private const string HostKey = "Host";
    private const string PortKey = "Port";
    private const string OidcJwtKey = "OidcJwt";
    private const string OidcJwtHostKey = "OidcJwtHost";
    private const string OidcJwtPortKey = "OidcJwtPort";

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

    public static bool TryLoadOidcJwt(string host, string port, out string jwtToken)
    {
        jwtToken = string.Empty;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
            return false;

        try
        {
            var prefs = GetPreferences();
            var storedToken = prefs?.GetString(OidcJwtKey, null)?.Trim();
            var storedHost = prefs?.GetString(OidcJwtHostKey, null)?.Trim();
            var storedPort = prefs?.GetString(OidcJwtPortKey, null)?.Trim();

            if (string.IsNullOrWhiteSpace(storedToken) ||
                string.IsNullOrWhiteSpace(storedHost) ||
                string.IsNullOrWhiteSpace(storedPort))
            {
                return false;
            }

            if (!string.Equals(storedHost, host.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(storedPort, port.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            jwtToken = storedToken;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SaveOidcJwt(string host, string port, string jwtToken)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(port) ||
            string.IsNullOrWhiteSpace(jwtToken))
        {
            return;
        }

        var prefs = GetPreferences();
        if (prefs == null)
            return;

        var editor = prefs.Edit();
        if (editor == null)
            return;

        using (editor)
        {
            editor.PutString(OidcJwtHostKey, host.Trim());
            editor.PutString(OidcJwtPortKey, port.Trim());
            editor.PutString(OidcJwtKey, jwtToken.Trim());
            editor.Apply();
        }
    }

    public static void ClearOidcJwt()
    {
        var prefs = GetPreferences();
        if (prefs == null)
            return;

        var editor = prefs.Edit();
        if (editor == null)
            return;

        using (editor)
        {
            editor.Remove(OidcJwtKey);
            editor.Remove(OidcJwtHostKey);
            editor.Remove(OidcJwtPortKey);
            editor.Apply();
        }
    }

    private static ISharedPreferences? GetPreferences()
    {
        var context = global::Android.App.Application.Context;
        return context?.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }
}
