using System;
using System.Collections.Generic;
using System.IO;
using McpServer.Client;

namespace McpServerManager.Services;

internal static class McpServerRestClientFactory
{
    private const string MarkerFileName = "AGENTS-README-FIRST.yaml";

    public static McpServerClient Create(string baseUrl, TimeSpan timeout, string? apiKey = null)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        var resolvedApiKey = string.IsNullOrWhiteSpace(apiKey)
            ? TryResolveApiKey(normalizedBaseUrl)
            : apiKey.Trim();

        return McpServerClientFactory.Create(new McpServerClientOptions
        {
            BaseUrl = new Uri(normalizedBaseUrl, UriKind.Absolute),
            ApiKey = string.IsNullOrWhiteSpace(resolvedApiKey) ? null : resolvedApiKey,
            Timeout = timeout
        });
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));

        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new ArgumentException("Base URL must be an absolute URI.", nameof(baseUrl));

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static string? TryResolveApiKey(string expectedBaseUrl)
    {
        foreach (var root in GetMarkerCandidateRoots())
        {
            if (TryResolveApiKeyForRoot(root, expectedBaseUrl) is { Length: > 0 } apiKey)
                return apiKey;
        }

        return null;
    }

    private static IEnumerable<string> GetMarkerCandidateRoots()
    {
        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
            yield return Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            yield return AppContext.BaseDirectory;
    }

    private static string? TryResolveApiKeyForRoot(string rootPath, string expectedBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return null;

        string fullRootPath;
        try
        {
            fullRootPath = Path.GetFullPath(rootPath.Trim());
        }
        catch
        {
            return null;
        }

        var markerPath = Path.Combine(fullRootPath, MarkerFileName);
        if (!File.Exists(markerPath))
            return null;

        try
        {
            string? markerBaseUrl = null;
            string? apiKey = null;
            foreach (var rawLine in File.ReadLines(markerPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (markerBaseUrl == null && TryParseTopLevelScalar(line, "baseUrl", out var parsedBaseUrl))
                    markerBaseUrl = parsedBaseUrl;
                else if (apiKey == null && TryParseTopLevelScalar(line, "apiKey", out var parsedApiKey))
                    apiKey = parsedApiKey;

                if (markerBaseUrl != null && apiKey != null)
                    break;
            }

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(markerBaseUrl))
                return null;

            var normalizedMarkerBaseUrl = NormalizeBaseUrl(markerBaseUrl);
            return string.Equals(normalizedMarkerBaseUrl, expectedBaseUrl, StringComparison.OrdinalIgnoreCase)
                ? apiKey
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseTopLevelScalar(string line, string key, out string? value)
    {
        value = null;
        var prefix = key + ":";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var scalar = line.Substring(prefix.Length).Trim();
        if (scalar.Length == 0)
            return false;

        if ((scalar.StartsWith('"') && scalar.EndsWith('"')) ||
            (scalar.StartsWith('\'') && scalar.EndsWith('\'')))
        {
            scalar = scalar.Substring(1, scalar.Length - 2);
        }

        value = scalar.Trim();
        return value.Length > 0;
    }
}
