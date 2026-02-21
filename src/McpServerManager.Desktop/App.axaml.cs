using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.Desktop.Services;
using McpServerManager.Desktop.Views;
using System;
using System.Linq;

namespace McpServerManager.Desktop;

public partial class App : Application
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("App");
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            try
            {
                var clipboardService = new DesktopClipboardService(desktop);
                var vm = new MainWindowViewModel(clipboardService);
                var window = new MainWindow { DataContext = vm };
                desktop.MainWindow = window;
                window.Show();
                window.Activate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainWindow creation failed");
                System.IO.File.WriteAllText("crash.log", ex.ToString());
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
