using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.Desktop.Views;
using Xunit;

namespace McpServerManager.Desktop.Tests;

public sealed class DesktopVoiceStartupIntegrationTests
{
    [AvaloniaFact]
    public async Task DesktopStartup_ToVoiceStartButton_InvokesStartFlow()
    {
        await EnsureHeadlessAppResourcesAsync();

        var vm = new MainWindowViewModel(
            new TestClipboardService(),
            mcpBaseUrl: "http://127.0.0.1:1",
            mcpApiKey: null,
            bearerToken: null,
            systemNotificationService: NoOpSystemNotificationService.Instance)
        {
            SaveWorkspaceKey = _ => { },
            LoadWorkspaceKey = () => null
        };

        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            vm.InitializeAfterWindowShown();

            await PumpUiAsync();

            var tabControl = window.FindControl<TabControl>("MainTabControl");
            Assert.NotNull(tabControl);
            tabControl.SelectedIndex = 2; // Voice tab
            await PumpUiAsync();

            var voiceView = window.FindControl<UserControl>("VoiceView");
            Assert.NotNull(voiceView);

            var startButton = voiceView.FindControl<Button>("ChatToggleButton");
            Assert.NotNull(startButton);

            var initialStatus = vm.VoiceConversationViewModel.StatusText;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });

            await PumpUiAsync();

            var status = vm.VoiceConversationViewModel.StatusText;
            Assert.False(string.Equals(status, initialStatus, StringComparison.Ordinal));
            Assert.False(string.Equals(status, "Voice ready", StringComparison.Ordinal));
            Assert.True(
                status.Contains("Creating", StringComparison.OrdinalIgnoreCase)
                || status.Contains("not ready", StringComparison.OrdinalIgnoreCase)
                || status.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || status.Contains("error", StringComparison.OrdinalIgnoreCase),
                $"Unexpected voice status after Start click: '{status}'");
        }
        finally
        {
            window.Close();
        }
    }

    private static Task EnsureHeadlessAppResourcesAsync()
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current == null)
            {
                AppBuilder.Configure<TestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            if (!Application.Current!.Styles.OfType<FluentTheme>().Any())
            {
                Application.Current.Styles.Insert(0, new FluentTheme());
            }
        }).GetTask();
    }

    private static async Task PumpUiAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    private sealed class TestApp : Application
    {
    }
}
