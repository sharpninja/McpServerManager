using System;
using System.IO;
using System.Text.Json;
using McpServerManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Desktop.Services;

/// <summary>Persists the Desktop connect-dialog host/port for reuse on next startup.</summary>
internal static class DesktopConnectionPreferencesService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("DesktopConnectionPreferences");
    private const string FileName = "connection.json";
    private const string DefaultWorkspaceKey = "__DEFAULT__";

    private sealed record ConnectionPrefs(string Host, string Port, string? OidcJwt = null,
        string? OidcJwtHost = null, string? OidcJwtPort = null, string? WorkspaceKey = null);

    public static bool TryLoad(out string host, out string port)
    {
        host = string.Empty;
        port = string.Empty;

        try
        {
            var prefs = Read();
            if (prefs is null || string.IsNullOrWhiteSpace(prefs.Host) || string.IsNullOrWhiteSpace(prefs.Port))
                return false;

            host = prefs.Host;
            port = prefs.Port;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load connection preferences");
            return false;
        }
    }

    public static void Save(string host, string port)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
            return;

        try
        {
            var existing = Read();
            var prefs = existing is null
                ? new ConnectionPrefs(host.Trim(), port.Trim())
                : existing with { Host = host.Trim(), Port = port.Trim() };
            Write(prefs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save connection preferences");
        }
    }

    public static bool TryLoadOidcJwt(string host, string port, out string jwtToken)
    {
        jwtToken = string.Empty;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
            return false;

        try
        {
            var prefs = Read();
            if (prefs is null
                || string.IsNullOrWhiteSpace(prefs.OidcJwt)
                || !string.Equals(prefs.OidcJwtHost, host.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(prefs.OidcJwtPort, port.Trim(), StringComparison.Ordinal))
                return false;

            jwtToken = prefs.OidcJwt;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load OIDC JWT from connection preferences");
            return false;
        }
    }

    public static void SaveOidcJwt(string host, string port, string jwtToken)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(jwtToken))
            return;

        try
        {
            var existing = Read() ?? new ConnectionPrefs(host.Trim(), port.Trim());
            var prefs = existing with
            {
                OidcJwtHost = host.Trim(),
                OidcJwtPort = port.Trim(),
                OidcJwt = jwtToken.Trim()
            };
            Write(prefs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save OIDC JWT to connection preferences");
        }
    }

    public static void ClearOidcJwt()
    {
        try
        {
            var existing = Read();
            if (existing is null) return;
            Write(existing with { OidcJwt = null, OidcJwtHost = null, OidcJwtPort = null });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear OIDC JWT from connection preferences");
        }
    }

    public static void SaveWorkspaceKey(string? key)
    {
        try
        {
            var existing = Read();
            if (existing is null) return;

            var normalized = string.IsNullOrWhiteSpace(key)
                ? null
                : key.Trim();
            if (string.Equals(normalized, DefaultWorkspaceKey, StringComparison.OrdinalIgnoreCase))
                normalized = null;

            Write(existing with { WorkspaceKey = normalized });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save workspace key to connection preferences");
        }
    }

    public static string? LoadWorkspaceKey()
    {
        try
        {
            var prefs = Read();
            var key = prefs?.WorkspaceKey?.Trim();
            if (string.IsNullOrWhiteSpace(key) ||
                string.Equals(key, DefaultWorkspaceKey, StringComparison.OrdinalIgnoreCase))
                return null;

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace key from connection preferences");
            return null;
        }
    }

    private static string GetFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServerManager");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    private static ConnectionPrefs? Read()
    {
        var path = GetFilePath();
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConnectionPrefs>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static void Write(ConnectionPrefs prefs)
    {
        var path = GetFilePath();
        var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
