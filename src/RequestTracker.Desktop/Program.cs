using Avalonia;
using System;
using System.IO;
using Microsoft.Extensions.Logging;
using RequestTracker.Core;
using RequestTracker.Core.Services;

namespace RequestTracker.Desktop;

sealed class Program
{
    private static readonly string HtmlCacheDir = AppSettings.ResolveHtmlCacheDirectory();
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("Program");

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            RemoveGeneratedHtmlFiles();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL CRASH");
            File.WriteAllText("crash.log", ex.ToString());
            throw;
        }
    }

    private static void RemoveGeneratedHtmlFiles()
    {
        try
        {
            if (!Directory.Exists(HtmlCacheDir))
                return;
            Directory.Delete(HtmlCacheDir, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not remove HTML cache: {Message}", ex.Message);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
