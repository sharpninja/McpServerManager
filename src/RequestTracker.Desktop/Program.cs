using Avalonia;
using System;
using System.IO;
using RequestTracker.Core;

namespace RequestTracker.Desktop;

sealed class Program
{
    private static readonly string HtmlCacheDir = AppSettings.ResolveHtmlCacheDirectory();

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
            Console.Error.WriteLine($"FATAL CRASH: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"Could not remove HTML cache: {ex.Message}");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
