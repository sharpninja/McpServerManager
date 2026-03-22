using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using McpServerManager.Android.Services;
using McpServerManager.Android.Views;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using UiDispatcherHost = McpServer.UI.Core.Services.UiDispatcherHost;

namespace McpServerManager.Android;

public partial class App : Application
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("App");
    private ServiceProvider? _connectionServices;
    private AndroidMainWindowSession? _mainWindowSession;

    public override void Initialize()
    {
        AndroidCrashDiagnostics.ExecuteFatal(
            "App.Initialize",
            () => AvaloniaXamlLoader.Load(this),
            "Android Avalonia application crashed while loading XAML resources.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        void Core()
        {
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                DisableAvaloniaDataAnnotationValidation();
                AndroidLogcatBridge.EnsureInitialized();
                AndroidCrashDiagnostics.ReplayPendingDiagnostics();
                AndroidOidcJwtCacheInvalidationMonitor.EnsureInitialized();
                VoiceChatSettingsService.Instance.ConfigureStore(new AndroidVoiceChatSettingsStore());
                global::Android.Util.Log.Info("McpSM", "[App] OnFrameworkInitializationCompleted entered (Android)");

                try
                {
                    var uiDispatcher = new AvaloniaUiDispatcherService();
                    UiDispatcherHost.Configure(uiDispatcher);
                    _connectionServices ??= AndroidAppServiceFactory.BuildConnectionProvider(uiDispatcher);
                    var connectionVm = _connectionServices.GetRequiredService<ConnectionViewModel>();
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

                    void OpenMainView(string mcpBaseUrl, string? mcpApiKey, string? bearerToken)
                    {
                        AndroidMainWindowSession? nextSession = null;

                        try
                        {
                            _logger.LogInformation("OpenMainView: entered for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}", mcpBaseUrl, !string.IsNullOrWhiteSpace(mcpApiKey), !string.IsNullOrWhiteSpace(bearerToken));
                            if (Uri.TryCreate(mcpBaseUrl, UriKind.Absolute, out var uri))
                            {
                                var saveHost = uri.Host;
                                var savePort = uri.Port.ToString(CultureInfo.InvariantCulture);
                                _logger.LogInformation("OpenMainView: saving connection {Host}:{Port}", saveHost, savePort);
                                AndroidConnectionPreferencesService.Save(saveHost, savePort);
                                _logger.LogInformation("OpenMainView: save call completed for {Host}:{Port}", saveHost, savePort);
                            }
                            else
                            {
                                _logger.LogWarning("OpenMainView: Uri.TryCreate failed for '{McpBaseUrl}' — connection not saved", mcpBaseUrl);
                            }

                            nextSession = AndroidAppServiceFactory.CreateMainWindowSession(uiDispatcher, mcpBaseUrl, mcpApiKey, bearerToken);
                            var vm = nextSession.ViewModel;
                            vm.SaveWorkspaceKey = AndroidConnectionPreferencesService.SaveWorkspaceKey;
                            vm.LoadWorkspaceKey = AndroidConnectionPreferencesService.LoadWorkspaceKey;
                            vm.LogoutRequested += (_, _) =>
                            {
                                _logger.LogInformation("Logout requested; clearing tokens and returning to connection dialog");
                                connectionVm.LogoutCommand.Execute(null);
                                AndroidConnectionPreferencesService.ClearOidcJwt();
                                connectionVm.IsConnecting = false;
                                connectionVm.ErrorMessage = "";
                                _mainWindowSession?.Dispose();
                                _mainWindowSession = null;
                                singleView.MainView = connectionView;
                            };

                            _mainWindowSession?.Dispose();
                            _mainWindowSession = nextSession;
                            nextSession = null;
                            singleView.MainView = new AdaptiveMainView { DataContext = vm };
                            vm.InitializeAfterWindowShown();
                        }
                        catch (Exception ex)
                        {
                            nextSession?.Dispose();
                            AndroidCrashDiagnostics.RecordDiagnosticEvent(
                                "App.OpenMainView",
                                ex,
                                "Failed while constructing Android main view after connection success.");
                            _logger.LogError(ex, "Connection failed");
                            connectionVm.ErrorMessage = $"Connection failed: {ex.Message}";
                            connectionVm.IsConnecting = false;
                            singleView.MainView = connectionView;
                        }
                    }

                    connectionVm.Connected += connection =>
                    {
                        _logger.LogInformation("Connected event fired for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}", connection.BaseUrl, !string.IsNullOrWhiteSpace(connection.ApiKey), !string.IsNullOrWhiteSpace(connection.BearerToken));
                        uiDispatcher.Post(() => OpenMainView(connection.BaseUrl, connection.ApiKey, connection.BearerToken));
                    };

                    if (AndroidConnectionPreferencesService.TryLoad(out var savedHost, out var savedPort))
                    {
                        _logger.LogInformation("Startup: loaded saved connection {Host}:{Port}; auto-connecting", savedHost, savedPort);
                        connectionVm.Host = savedHost;
                        connectionVm.Port = savedPort;
                        connectionVm.ErrorMessage = "";
                        _logger.LogInformation("Startup: VM updated with saved Host={Host}, Port={Port}; executing ConnectCommand", connectionVm.Host, connectionVm.Port);
                        connectionVm.ConnectCommand.Execute(null);
                    }
                    else
                    {
                        _logger.LogInformation("Startup: no saved connection found; showing connection dialog with defaults Host={Host}, Port={Port}", connectionVm.Host, connectionVm.Port);
                    }
                }
                catch (Exception ex)
                {
                    AndroidCrashDiagnostics.RecordDiagnosticEvent(
                        "App.OnFrameworkInitializationCompleted",
                        ex,
                        "Android application initialization failed after Avalonia framework startup.");
                    _logger.LogError(ex, "Android init failed");
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "App.OnFrameworkInitializationCompleted",
            Core,
            "Android Avalonia application crashed while completing framework initialization.");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
