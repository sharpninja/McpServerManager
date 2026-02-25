using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class TabletMainView : UserControl
{
    public TabletMainView()
    {
        InitializeComponent();
    }

    private void StatusMessageText_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.StatusMessage))
            return;

        StatusDialogText.Text = vm.StatusMessage;
        StatusDialogOverlay.IsVisible = true;
        e.Handled = true;
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
