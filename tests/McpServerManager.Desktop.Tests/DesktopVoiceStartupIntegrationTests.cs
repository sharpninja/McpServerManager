using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using McpServerManager.Android.Views;
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

            var voiceToggle = window.FindControl<ToggleButton>("VoiceFlyoutToggleButton");
            Assert.NotNull(voiceToggle);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                voiceToggle.IsChecked = true;
            });
            await PumpUiAsync();

            var voiceView = window.FindControl<UserControl>("VoiceFlyoutView");
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

    [AvaloniaFact]
    public async Task DesktopVoiceFlyout_CloseButton_HidesFlyoutWithoutThrowing()
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

            var voiceToggle = window.FindControl<ToggleButton>("VoiceFlyoutToggleButton");
            var voiceBorder = window.FindControl<Border>("VoiceFlyoutBorder");
            var closeButton = window.FindControl<Button>("VoiceFlyoutCloseButton");

            Assert.NotNull(voiceToggle);
            Assert.NotNull(voiceBorder);
            Assert.NotNull(closeButton);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                voiceToggle.IsChecked = true;
            });
            await PumpUiAsync();
            Assert.True(voiceBorder.IsVisible);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            await PumpUiAsync();

            Assert.False(voiceBorder.IsVisible);
            Assert.False(voiceToggle.IsChecked == true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SimplifiedVoiceView_DefaultLayout_DoesNotShiftManualInputRows()
    {
        await EnsureHeadlessAppResourcesAsync();

        var view = new SimplifiedVoiceView();
        var window = new Window
        {
            Width = 480,
            Height = 800,
            Content = view
        };

        try
        {
            window.Show();
            await PumpUiAsync();

            var inputRowsPanel = view.FindControl<StackPanel>("ManualInputRowsPanel");
            Assert.NotNull(inputRowsPanel);
            Assert.True(Math.Abs(inputRowsPanel.Margin.Top) < 0.5, $"Expected no top offset, but got {inputRowsPanel.Margin.Top}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SimplifiedVoiceView_PhoneLayout_ShiftsManualInputRowsByManualRowHeight()
    {
        await EnsureHeadlessAppResourcesAsync();

        var view = new SimplifiedVoiceView
        {
            IsPhoneVoiceLayout = true
        };
        var window = new Window
        {
            Width = 480,
            Height = 800,
            Content = view
        };

        try
        {
            window.Show();
            await PumpUiAsync();

            var inputRowsPanel = view.FindControl<StackPanel>("ManualInputRowsPanel");
            var manualTextEntryBorder = view.FindControl<Border>("ManualTextEntryBorder");
            Assert.NotNull(inputRowsPanel);
            Assert.NotNull(manualTextEntryBorder);
            Assert.True(manualTextEntryBorder.Bounds.Height > 0, "Expected the manual text entry row to be measured.");
            Assert.True(
                Math.Abs(inputRowsPanel.Margin.Top + manualTextEntryBorder.Bounds.Height) < 1.0,
                $"Expected top offset {-manualTextEntryBorder.Bounds.Height}, but got {inputRowsPanel.Margin.Top}.");
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
