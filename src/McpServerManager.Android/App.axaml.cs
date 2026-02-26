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
            AndroidLogcatBridge.EnsureInitialized();
            AndroidOidcJwtCacheInvalidationMonitor.EnsureInitialized();
            global::Android.Util.Log.Info("McpSM", "[App] OnFrameworkInitializationCompleted entered (Android)");

            try
            {
                var connectionVm = new ConnectionViewModel();
                connectionVm.SetExternalUrlOpener(AndroidBrowserService.TryOpenUrl);
                connectionVm.SetOidcPostTokenForegroundActivator(AndroidBrowserService.TryBringAppToForeground);
                connectionVm.SetQrCodeScanner(AndroidQrScannerService.ScanQrCodeAsync);
                connectionVm.SetOidcTokenCacheAccessors(
                    () => AndroidConnectionPreferencesService.TryLoadOidcJwt(connectionVm.Host, connectionVm.Port, out var jwtToken)
                        ? jwtToken
                        : null,
                    token =>
                    {
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            AndroidConnectionPreferencesService.ClearOidcJwt();
                            return;
                        }

                        AndroidConnectionPreferencesService.SaveOidcJwt(connectionVm.Host, connectionVm.Port, token);
                    });
                var connectionView = new ConnectionDialogView { DataContext = connectionVm };
                singleView.MainView = connectionView;
                var persistNextConnection = true;

                void OpenMainView(string mcpBaseUrl, string? mcpApiKey, string? bearerToken, bool persistConnection)
                {
                    try
                    {
                        _logger.LogInformation("Opening main Android view for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}, PersistConnection={PersistConnection}", mcpBaseUrl, !string.IsNullOrWhiteSpace(mcpApiKey), !string.IsNullOrWhiteSpace(bearerToken), persistConnection);
                        if (persistConnection && Uri.TryCreate(mcpBaseUrl, UriKind.Absolute, out var uri))
                        {
                            AndroidConnectionPreferencesService.Save(
                                uri.Host,
                                uri.Port.ToString(CultureInfo.InvariantCulture));
                        }

                        var clipboardService = new AndroidClipboardService();
                        var vm = new MainWindowViewModel(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken);
                        vm.LogoutRequested += (_, _) =>
                        {
                            _logger.LogInformation("Logout requested; clearing tokens and returning to connection dialog");
                            AndroidConnectionPreferencesService.ClearOidcJwt();
                            connectionVm.IsConnecting = false;
                            connectionVm.ErrorMessage = "";
                            persistNextConnection = true;
                            singleView.MainView = connectionView;
                        };
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

                connectionVm.Connected += connection =>
                {
                    _logger.LogInformation("Connection dialog signaled Connected for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}", connection.BaseUrl, !string.IsNullOrWhiteSpace(connection.ApiKey), !string.IsNullOrWhiteSpace(connection.BearerToken));
                    var shouldPersist = persistNextConnection;
                    persistNextConnection = true;
                    OpenMainView(connection.BaseUrl, connection.ApiKey, connection.BearerToken, persistConnection: shouldPersist);
                };

                if (AndroidConnectionPreferencesService.TryLoad(out var savedHost, out var savedPort))
                {
                    _logger.LogInformation("Loaded saved Android connection {Host}:{Port}; auto-connecting", savedHost, savedPort);
                    connectionVm.Host = savedHost;
                    connectionVm.Port = savedPort;
                    connectionVm.ErrorMessage = "";
                    persistNextConnection = false;
                    connectionVm.ConnectCommand.Execute(null);
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
