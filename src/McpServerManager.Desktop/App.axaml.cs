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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        WireGlobalExceptionHandlers();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            try
            {
                var connectionVm = new ConnectionViewModel();
                connectionVm.Host = "localhost";

                connectionVm.SetExternalUrlOpener(url =>
                {
                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            Process.Start("xdg-open", url);
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                            Process.Start("open", url);
                        return true;
                    }
                    catch { return false; }
                });

                connectionVm.SetOidcTokenCacheAccessors(
                    () => DesktopConnectionPreferencesService.TryLoadOidcJwt(connectionVm.Host, connectionVm.Port, out var jwt)
                        ? jwt : null,
                    token =>
                    {
                        if (string.IsNullOrWhiteSpace(token))
                            DesktopConnectionPreferencesService.ClearOidcJwt();
                        else
                            DesktopConnectionPreferencesService.SaveOidcJwt(connectionVm.Host, connectionVm.Port, token);
                    });

                var connectionWindow = new ConnectionWindow { DataContext = connectionVm };
                desktop.MainWindow = connectionWindow;
                var persistNextConnection = true;

                void OpenMainView(string mcpBaseUrl, string? mcpApiKey, string? bearerToken, bool persistConnection)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Opening main Desktop view for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}",
                            mcpBaseUrl, !string.IsNullOrWhiteSpace(mcpApiKey), !string.IsNullOrWhiteSpace(bearerToken));

                        if (persistConnection && Uri.TryCreate(mcpBaseUrl, UriKind.Absolute, out var uri))
                        {
                            DesktopConnectionPreferencesService.Save(
                                uri.Host,
                                uri.Port.ToString(CultureInfo.InvariantCulture));
                        }

                        var clipboardService = new DesktopClipboardService(desktop);
                        var notificationService = new DesktopSystemNotificationService();
                        var vm = new MainWindowViewModel(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken, notificationService);
                        var mainWindow = new MainWindow { DataContext = vm };

                        vm.LogoutRequested += (_, _) =>
                        {
                            _logger.LogInformation("Logout requested; returning to connection dialog");
                            connectionVm.LogoutCommand.Execute(null);
                            DesktopConnectionPreferencesService.ClearOidcJwt();
                            connectionVm.IsConnecting = false;
                            connectionVm.ErrorMessage = "";
                            persistNextConnection = true;
                            var newConnectionWindow = new ConnectionWindow { DataContext = connectionVm };
                            desktop.MainWindow = newConnectionWindow;
                            newConnectionWindow.Show();
                            mainWindow.Close();
                        };

                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        mainWindow.Activate();
                        connectionWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Connection failed");
                        connectionVm.ErrorMessage = $"Connection failed: {ex.Message}";
                        connectionVm.IsConnecting = false;
                    }
                }

                connectionVm.Connected += connection =>
                {
                    _logger.LogInformation(
                        "Connection dialog signaled Connected for {McpBaseUrl}. TokenPresent={HasToken}, BearerPresent={HasBearer}",
                        connection.BaseUrl, !string.IsNullOrWhiteSpace(connection.ApiKey), !string.IsNullOrWhiteSpace(connection.BearerToken));
                    var shouldPersist = persistNextConnection;
                    persistNextConnection = true;
                    OpenMainView(connection.BaseUrl, connection.ApiKey, connection.BearerToken, persistConnection: shouldPersist);
                };

                connectionWindow.Show();

                // Auto-connect using saved preferences or appsettings.config defaults
                if (DesktopConnectionPreferencesService.TryLoad(out var savedHost, out var savedPort))
                {
                    _logger.LogInformation("Loaded saved Desktop connection {Host}:{Port}; auto-connecting", savedHost, savedPort);
                    connectionVm.Host = savedHost;
                    connectionVm.Port = savedPort;
                    persistNextConnection = false;
                    connectionVm.ConnectCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "App initialization failed");
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

    private static void WireGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            _logger.LogError(ex, "Unhandled exception");
            StatusViewModel.Instance.AddStatus(ex?.ToString() ?? args.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger.LogError(args.Exception, "Unobserved task exception");
            StatusViewModel.Instance.AddStatus(args.Exception.ToString());
            args.SetObserved();
        };
    }
}
