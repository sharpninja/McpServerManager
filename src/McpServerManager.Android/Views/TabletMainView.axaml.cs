using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class TabletMainView : UserControl
{
    private const int StatusLongPressThresholdMilliseconds = 600;
    private DateTimeOffset? _statusPointerPressedAt;

    public TabletMainView()
    {
        InitializeComponent();
    }

    private void StatusMessageText_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.StatusMessage))
            return;

        _statusPointerPressedAt = DateTimeOffset.UtcNow;
        e.Handled = true;
    }

    private async void StatusMessageText_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.StatusMessage))
        {
            _statusPointerPressedAt = null;
            return;
        }

        var pressedAt = _statusPointerPressedAt;
        _statusPointerPressedAt = null;
        if (pressedAt is null)
            return;

        if ((DateTimeOffset.UtcNow - pressedAt.Value).TotalMilliseconds >= StatusLongPressThresholdMilliseconds)
        {
            await CopyStatusToClipboardAsync(vm.StatusMessage);
            e.Handled = true;
            return;
        }

        ShowStatusDialog(vm.StatusMessage);
        e.Handled = true;
    }

    private void StatusMessageText_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _statusPointerPressedAt = null;
    }

    private async Task CopyStatusToClipboardAsync(string statusText)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(statusText);
    }

    private void ShowStatusDialog(string statusText)
    {
        StatusDialogText.Text = statusText;
        StatusDialogOverlay.IsVisible = true;
    }

    private void CloseStatusDialog_OnClick(object? sender, RoutedEventArgs e)
    {
        StatusDialogOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void StatusDialogOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        StatusDialogOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void StatusDialogContent_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
