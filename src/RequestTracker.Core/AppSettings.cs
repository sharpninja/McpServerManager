using System;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Linq;
using RequestTracker.Core.Converters;

namespace RequestTracker.Core;

public sealed class AppSettings
{
    private const string ConfigFileName = "appsettings.config";

    public PathSettings Paths { get; init; } = new();
    public McpSettings Mcp { get; init; } = new();

    private static readonly Lazy<AppSettings> CurrentValue = new(Load);

    public static AppSettings Current => CurrentValue.Value;

    public static string ResolveSessionsRootPath()
        => ResolveRequiredPath(Current.Paths.SessionsRootPath, "Paths.SessionsRootPath");

    public static string ResolveHtmlCacheDirectory()
    {
        var dir = Current.Paths.HtmlCacheDirectory;
        if (string.IsNullOrWhiteSpace(dir))
        {
            // Fallback: use a temp directory
            return Path.Combine(Path.GetTempPath(), "RequestTracker_Cache");
        }
        return ResolveRequiredPath(dir, "Paths.HtmlCacheDirectory");
    }

    public static string? ResolveCssFallbackPath()
        => ResolveOptionalPath(Current.Paths.CssFallbackPath);

    public static string ResolveMcpBaseUrl()
        => ResolveRequiredUrl(Current.Mcp.BaseUrl, "Mcp.BaseUrl");

    private static AppSettings Load()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            // On Android there is no appsettings.config; return defaults.
            if (OperatingSystem.IsAndroid())
                return new AppSettings();
            throw new FileNotFoundException("Missing appsettings.config. Ensure it is copied next to the executable.", configPath);
        }

        string json = File.ReadAllText(configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (settings == null)
            throw new InvalidOperationException("Failed to parse appsettings.config.");

        return settings;
    }

    private static string ResolveRequiredPath(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Setting '{settingName}' is required.");
        return NormalizePath(value);
    }

    private static string? ResolveOptionalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return NormalizePath(value);
    }

    private static string ResolveRequiredUrl(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Setting '{settingName}' is required.");
        var url = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Setting '{settingName}' must be an absolute http/https URL.");
        }

        // In Linux/WSL, localhost in app config points to the container/WSL namespace.
        // Prefer the host-side nameserver IP so the desktop app can reach services running on the host.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1"))
        {
            var hostIp = TryGetHostNameserverIp();
            if (!string.IsNullOrEmpty(hostIp) && Uri.CheckHostName(hostIp) != UriHostNameType.Unknown)
            {
                var ub = new UriBuilder(uri) { Host = hostIp };
                return ub.Uri.ToString().TrimEnd('/');
            }
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string? TryGetHostNameserverIp()
    {
        try
        {
            const string resolvConf = "/etc/resolv.conf";
            if (!File.Exists(resolvConf))
                return null;

            var line = File.ReadLines(resolvConf)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return null;

            return parts[1];
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string value)
    {
        string expanded = Environment.ExpandEnvironmentVariables(value.Trim());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && PathConverter.IsWindowsPath(expanded))
            return Path.GetFullPath(PathConverter.ToWslPath(expanded));
        if (!Path.IsPathRooted(expanded))
            expanded = Path.Combine(AppContext.BaseDirectory, expanded);
        return Path.GetFullPath(expanded);
    }

    public sealed class PathSettings
    {
        public string? SessionsRootPath { get; init; }
        public string? HtmlCacheDirectory { get; init; }
        public string? CssFallbackPath { get; init; }
    }

    public sealed class McpSettings
    {
        public string? BaseUrl { get; init; }
    }
}
