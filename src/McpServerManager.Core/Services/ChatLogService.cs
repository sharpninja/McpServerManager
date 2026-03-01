using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.Services;

/// <summary>
/// Persists voice chat request-response pairs to a rolling YAML log file.
/// Each exchange is appended as a YAML document separated by "---".
/// </summary>
public sealed class ChatLogService
{
    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("ChatLog");
    private static readonly Lazy<ChatLogService> LazyInstance = new(() => new ChatLogService());

    private const string LogFileName = "voice-chat.yaml";
    private readonly object _lock = new();

    /// <summary>Singleton instance shared across the app.</summary>
    public static ChatLogService Instance => LazyInstance.Value;

    private ChatLogService() { }

    /// <summary>Gets the path to the rolling chat log file.</summary>
    public static string GetLogFilePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServerManager");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, LogFileName);
    }

    /// <summary>Appends a completed request-response exchange to the log.</summary>
    public void LogExchange(ChatLogEntry entry)
    {
        try
        {
            var yaml = FormatYaml(entry);
            lock (_lock)
            {
                File.AppendAllText(GetLogFilePath(), yaml);
            }
            Logger.LogDebug("Logged chat exchange: {TurnLength} chars", entry.ResponseText?.Length ?? 0);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to write chat log entry");
        }
    }

    private static string FormatYaml(ChatLogEntry e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        if (e.SessionId is not null)
            sb.AppendLine($"session: {e.SessionId}");
        if (e.RequestTimestamp is not null)
            sb.AppendLine($"timestamp: {e.RequestTimestamp}");
        if (e.RequestText is not null)
            sb.AppendLine($"request: {YamlQuote(e.RequestText)}");
        if (e.ResponseText is not null)
            sb.AppendLine($"response: {YamlQuote(e.ResponseText)}");
        if (e.FirstResponseDuration is not null)
            sb.AppendLine($"first_response: {e.FirstResponseDuration}");
        if (e.TotalDuration is not null)
            sb.AppendLine($"total_duration: {e.TotalDuration}");
        if (e.FirstResponseMs.HasValue)
            sb.AppendLine($"first_response_ms: {e.FirstResponseMs.Value}");
        if (e.TotalMs.HasValue)
            sb.AppendLine($"total_ms: {e.TotalMs.Value}");
        if (!e.Success)
        {
            sb.AppendLine("success: false");
            if (e.Error is not null)
                sb.AppendLine($"error: {YamlQuote(e.Error)}");
        }
        return sb.ToString();
    }

    private static string YamlQuote(string value)
    {
        if (value.Contains('\n') || value.Contains('\r'))
        {
            // Use YAML literal block scalar for multiline
            var indented = value.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = indented.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine("|");
            foreach (var line in lines)
                sb.AppendLine($"    {line}");
            return sb.ToString().TrimEnd();
        }

        // Quote if contains special chars
        if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
            value.Contains('\'') || value.Contains('{') || value.Contains('}') ||
            value.Contains('[') || value.Contains(']'))
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        return value;
    }
}

/// <summary>A single request-response exchange in the voice chat log.</summary>
public sealed class ChatLogEntry
{
    /// <summary>Session ID for the voice conversation.</summary>
    public string? SessionId { get; set; }

    /// <summary>ISO 8601 timestamp when the user submitted the request.</summary>
    public string? RequestTimestamp { get; set; }

    /// <summary>The user's spoken or typed input text.</summary>
    public string? RequestText { get; set; }

    /// <summary>The assistant's full response text.</summary>
    public string? ResponseText { get; set; }

    /// <summary>Formatted duration from request to first response chunk.</summary>
    public string? FirstResponseDuration { get; set; }

    /// <summary>Formatted duration from request to final response.</summary>
    public string? TotalDuration { get; set; }

    /// <summary>First response latency in milliseconds.</summary>
    public long? FirstResponseMs { get; set; }

    /// <summary>Total response duration in milliseconds.</summary>
    public long? TotalMs { get; set; }

    /// <summary>Whether the exchange completed successfully.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Error message if the exchange failed.</summary>
    public string? Error { get; set; }
}
