using System.Text.Json;

namespace McpServerManager.Director;

/// <summary>
/// Persists Director CLI defaults (for non-workspace usage) under the user's profile.
/// </summary>
internal static class DirectorCliConfigStore
{
    private static readonly string s_configDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".mcpserver");

    private static readonly string s_defaultConfigPath = Path.Combine(s_configDir, "director.config.json");

    private static string ConfigPath =>
        Environment.GetEnvironmentVariable("DIRECTOR_CLI_CONFIG") is { Length: > 0 } overridePath
            ? overridePath
            : s_defaultConfigPath;

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static DirectorCliConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new DirectorCliConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<DirectorCliConfig>(json, s_jsonOpts) ?? new DirectorCliConfig();
        }
        catch
        {
            return new DirectorCliConfig();
        }
    }

    public static void Save(DirectorCliConfig config)
    {
        var configPath = ConfigPath;
        var configDir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configDir))
            Directory.CreateDirectory(configDir);
        var json = JsonSerializer.Serialize(config, s_jsonOpts);
        File.WriteAllText(configPath, json);
    }

    public static string GetConfigPath() => ConfigPath;
}

internal sealed class DirectorCliConfig
{
    public string? DefaultBaseUrl { get; set; }
}
