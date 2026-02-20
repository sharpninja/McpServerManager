using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using RequestTracker.Android.Services;
using RequestTracker.Android.Views;
using RequestTracker.Core.Services;
using RequestTracker.Core.ViewModels;
using System;
using System.Linq;

namespace RequestTracker.Android;

public partial class App : Application
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("App");
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
                var connectionVm = new ConnectionViewModel();
                var connectionView = new ConnectionDialogView { DataContext = connectionVm };
                singleView.MainView = connectionView;

                connectionVm.Connected += mcpBaseUrl =>
                {
                    try
                    {
                        var clipboardService = new AndroidClipboardService();
                        var vm = new MainWindowViewModel(clipboardService, mcpBaseUrl);
                        singleView.MainView = new AdaptiveMainView { DataContext = vm };
                        vm.InitializeAfterWindowShown();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Connection failed");
                        connectionVm.ErrorMessage = $"Connection failed: {ex.Message}";
                        connectionVm.IsConnecting = false;
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Android init failed");
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
