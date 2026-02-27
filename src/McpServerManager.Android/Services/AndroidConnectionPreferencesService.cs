using System;
using Android.Content;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

/// <summary>Persists the Android connect-dialog host/port for reuse on next startup.</summary>
internal static class AndroidConnectionPreferencesService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("AndroidConnectionPrefs");

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
            if (prefs == null)
            {
                _logger.LogWarning("TryLoad: SharedPreferences is null — cannot load saved connection");
                return false;
            }

            var storedHost = prefs.GetString(HostKey, null)?.Trim();
            var storedPort = prefs.GetString(PortKey, null)?.Trim();

            if (string.IsNullOrWhiteSpace(storedHost) || string.IsNullOrWhiteSpace(storedPort))
            {
                _logger.LogInformation(
                    "TryLoad: no saved connection found (Host={Host}, Port={Port})",
                    storedHost ?? "<null>", storedPort ?? "<null>");
                return false;
            }

            host = storedHost;
            port = storedPort;
            _logger.LogInformation("TryLoad: loaded saved connection {Host}:{Port}", host, port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryLoad: exception reading SharedPreferences");
            return false;
        }
    }

    public static void Save(string host, string port)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
        {
            _logger.LogWarning("Save: skipped — host or port is empty (Host={Host}, Port={Port})",
                host ?? "<null>", port ?? "<null>");
            return;
        }

        var trimmedHost = host.Trim();
        var trimmedPort = port.Trim();

        var prefs = GetPreferences();
        if (prefs == null)
        {
            _logger.LogError("Save: SharedPreferences is null — cannot persist connection {Host}:{Port}",
                trimmedHost, trimmedPort);
            return;
        }

        var editor = prefs.Edit();
        if (editor == null)
        {
            _logger.LogError("Save: SharedPreferences.Edit() returned null — cannot persist connection {Host}:{Port}",
                trimmedHost, trimmedPort);
            return;
        }

        using (editor)
        {
            editor.PutString(HostKey, trimmedHost);
            editor.PutString(PortKey, trimmedPort);
            var committed = editor.Commit();
            _logger.LogInformation("Save: Commit({Host}:{Port}) returned {Result}",
                trimmedHost, trimmedPort, committed);
        }

        // Verify the write by reading back immediately
        var verifyHost = prefs.GetString(HostKey, null);
        var verifyPort = prefs.GetString(PortKey, null);
        if (string.Equals(verifyHost, trimmedHost, StringComparison.Ordinal) &&
            string.Equals(verifyPort, trimmedPort, StringComparison.Ordinal))
        {
            _logger.LogInformation("Save: verified — read-back matches ({Host}:{Port})", verifyHost, verifyPort);
        }
        else
        {
            _logger.LogError(
                "Save: VERIFICATION FAILED — wrote {Host}:{Port} but read back {ReadHost}:{ReadPort}",
                trimmedHost, trimmedPort, verifyHost ?? "<null>", verifyPort ?? "<null>");
        }
    }

    public static bool TryLoadOidcJwt(string host, string port, out string jwtToken)
    {
        jwtToken = string.Empty;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
        {
            _logger.LogWarning("TryLoadOidcJwt: skipped — host or port is empty");
            return false;
        }

        try
        {
            var prefs = GetPreferences();
            if (prefs == null)
            {
                _logger.LogWarning("TryLoadOidcJwt: SharedPreferences is null");
                return false;
            }

            var storedToken = prefs.GetString(OidcJwtKey, null)?.Trim();
            var storedHost = prefs.GetString(OidcJwtHostKey, null)?.Trim();
            var storedPort = prefs.GetString(OidcJwtPortKey, null)?.Trim();

            if (string.IsNullOrWhiteSpace(storedToken) ||
                string.IsNullOrWhiteSpace(storedHost) ||
                string.IsNullOrWhiteSpace(storedPort))
            {
                _logger.LogInformation("TryLoadOidcJwt: no cached JWT found");
                return false;
            }

            if (!string.Equals(storedHost, host.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(storedPort, port.Trim(), StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "TryLoadOidcJwt: cached JWT host:port ({StoredHost}:{StoredPort}) does not match request ({Host}:{Port})",
                    storedHost, storedPort, host.Trim(), port.Trim());
                return false;
            }

            jwtToken = storedToken;
            _logger.LogInformation("TryLoadOidcJwt: loaded cached JWT for {Host}:{Port}", storedHost, storedPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryLoadOidcJwt: exception reading SharedPreferences");
            return false;
        }
    }

    public static void SaveOidcJwt(string host, string port, string jwtToken)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(port) ||
            string.IsNullOrWhiteSpace(jwtToken))
        {
            _logger.LogWarning("SaveOidcJwt: skipped — host, port, or token is empty");
            return;
        }

        var prefs = GetPreferences();
        if (prefs == null)
        {
            _logger.LogError("SaveOidcJwt: SharedPreferences is null");
            return;
        }

        var editor = prefs.Edit();
        if (editor == null)
        {
            _logger.LogError("SaveOidcJwt: SharedPreferences.Edit() returned null");
            return;
        }

        var trimmedHost = host.Trim();
        var trimmedPort = port.Trim();

        using (editor)
        {
            editor.PutString(OidcJwtHostKey, trimmedHost);
            editor.PutString(OidcJwtPortKey, trimmedPort);
            editor.PutString(OidcJwtKey, jwtToken.Trim());
            var committed = editor.Commit();
            _logger.LogInformation("SaveOidcJwt: Commit for {Host}:{Port} returned {Result}",
                trimmedHost, trimmedPort, committed);
        }
    }

    public static void ClearOidcJwt()
    {
        var prefs = GetPreferences();
        if (prefs == null)
        {
            _logger.LogWarning("ClearOidcJwt: SharedPreferences is null");
            return;
        }

        var editor = prefs.Edit();
        if (editor == null)
        {
            _logger.LogWarning("ClearOidcJwt: SharedPreferences.Edit() returned null");
            return;
        }

        using (editor)
        {
            editor.Remove(OidcJwtKey);
            editor.Remove(OidcJwtHostKey);
            editor.Remove(OidcJwtPortKey);
            var committed = editor.Commit();
            _logger.LogInformation("ClearOidcJwt: Commit returned {Result}", committed);
        }
    }

    private static ISharedPreferences? GetPreferences()
    {
        var context = global::Android.App.Application.Context;
        if (context == null)
        {
            _logger.LogError("GetPreferences: Application.Context is null");
            return null;
        }

        var prefs = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        if (prefs == null)
            _logger.LogError("GetPreferences: GetSharedPreferences returned null for '{Name}'", PreferencesName);

        return prefs;
    }
}
