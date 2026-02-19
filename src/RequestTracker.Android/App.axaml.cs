using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using RequestTracker.Android.Services;
using RequestTracker.Android.Views;
using RequestTracker.Core.ViewModels;
using System;
using System.Linq;

namespace RequestTracker.Android;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            DisableAvaloniaDataAnnotationValidation();

            try
            {
                var clipboardService = new AndroidClipboardService();
                var vm = new MainWindowViewModel(clipboardService);
                singleView.MainView = DeviceFormFactor.IsTablet()
                    ? new TabletMainView { DataContext = vm }
                    : (Avalonia.Controls.Control)new PhoneMainView { DataContext = vm };
                vm.InitializeAfterWindowShown();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Android init failed: {ex}");
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
