using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using McpServerManager.ViewModels;
using McpServerManager.Views;
using McpServerManager.Core.Services;
using Microsoft.Extensions.Logging;
using UiDispatcherHost = McpServerManager.UI.Core.Services.UiDispatcherHost;

namespace McpServerManager;

public partial class App : Application
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("App");
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            Avalonia.Controls.Window? window = null;
            try
            {
                UiDispatcherHost.Configure(new AvaloniaUiDispatcherService());
                window = new MainWindow();
                try
                {
                    window.DataContext = new MainWindowViewModel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ViewModel init failed (window will still show)");
                }

                desktop.MainWindow = window;
                window.Show();
                window.Activate();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "MainWindow creation failed");
                System.IO.File.WriteAllText("crash.log", ex.ToString());
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
