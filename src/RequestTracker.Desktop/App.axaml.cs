using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using RequestTracker.Core.ViewModels;
using RequestTracker.Desktop.Services;
using RequestTracker.Desktop.Views;
using System;
using System.Linq;

namespace RequestTracker.Desktop;

public partial class App : Application
{
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
                Console.Error.WriteLine($"MainWindow creation failed: {ex}");
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
