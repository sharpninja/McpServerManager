using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using McpServerManager.Android.Services;
using McpServerManager.Android.Views;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using System;
using System.Globalization;
using System.Linq;

namespace McpServerManager.Android;

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

                void OpenMainView(string mcpBaseUrl, bool persistConnection)
                {
                    try
                    {
                        if (persistConnection && Uri.TryCreate(mcpBaseUrl, UriKind.Absolute, out var uri))
                        {
                            AndroidConnectionPreferencesService.Save(
                                uri.Host,
                                uri.Port.ToString(CultureInfo.InvariantCulture));
                        }

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
                        singleView.MainView = connectionView;
                    }
                }

                connectionVm.Connected += mcpBaseUrl =>
                {
                    OpenMainView(mcpBaseUrl, persistConnection: true);
                };

                if (AndroidConnectionPreferencesService.TryLoad(out var savedHost, out var savedPort))
                {
                    connectionVm.Host = savedHost;
                    connectionVm.Port = savedPort;
                    connectionVm.ErrorMessage = "";
                    connectionVm.IsConnecting = true;
                    OpenMainView($"http://{savedHost}:{savedPort}", persistConnection: false);
                }
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
