using System;
using System.Threading;
using Android.Util;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

public static class AndroidLogcatBridge
{
    private const string Tag = "McpSM";
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        AppLogService.Instance.EntryAdded += OnEntryAdded;
        Log.Info(Tag, "AndroidLogcatBridge initialized");
    }

    private static void OnEntryAdded(LogEntry entry)
    {
        var source = string.IsNullOrWhiteSpace(entry.Source) ? "App" : entry.Source;
        var message = $"[{source}] {entry.Message ?? string.Empty}";
        var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var line in lines)
        {
            if (line.Length == 0)
                continue;

            switch (entry.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    Log.Debug(Tag, line);
                    break;
                case LogLevel.Information:
                    Log.Info(Tag, line);
                    break;
                case LogLevel.Warning:
                    Log.Warn(Tag, line);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Log.Error(Tag, line);
                    break;
                default:
                    Log.Info(Tag, line);
                    break;
            }
        }
    }
}
