using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using McpServer.UI.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class TabletMainView : UserControl
{
    private const int StatusLongPressThresholdMilliseconds = 600;
    private const double MinVoiceFlyoutWidth = 320;
    private const double MinVoiceFlyoutHeight = 240;
    private const double DefaultVoiceFlyoutWidth = 420;
    private const double DefaultVoiceFlyoutHeight = 300;

    private DateTimeOffset? _statusPointerPressedAt;
    private LayoutSettings _layoutSettings = new();
    private bool _isVoiceFlyoutOpen;
    private bool _isVoiceFlyoutPinned = true;
    private bool? _voiceFlyoutIsPortrait;
    private bool _isUpdatingVoiceFlyoutToggleState;

    public TabletMainView()
    {
        InitializeComponent();
        LoadSettings();
        _isVoiceFlyoutOpen = _layoutSettings.VoiceFlyoutIsOpen;
        _isVoiceFlyoutPinned = _layoutSettings.VoiceFlyoutIsPinned;
        SyncVoiceFlyoutToggleState();
        Loaded += OnLoaded;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnHostSizeChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = Math.Clamp(_layoutSettings.SelectedTabIndex, 0, Math.Max(0, MainTabControl.ItemCount - 1));
        ApplyVoiceFlyoutLayout(Bounds.Size);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SaveSettings();
    }

    private void OnHostSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        CaptureVoiceFlyoutSize();
        ApplyVoiceFlyoutLayout(e.NewSize);
        SaveSettings();
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

    private void OnMainTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _layoutSettings.SelectedTabIndex = MainTabControl.SelectedIndex;
        if (_isVoiceFlyoutOpen && !_isVoiceFlyoutPinned)
            SetVoiceFlyoutOpen(false);
        else
            SaveSettings();
    }

    private void OnVoiceFlyoutToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingVoiceFlyoutToggleState)
            return;

        SetVoiceFlyoutOpen(VoiceFlyoutToggleButton.IsChecked == true);
        e.Handled = true;
    }

    private void OnVoicePinToggleChanged(object? sender, RoutedEventArgs e)
    {
        _isVoiceFlyoutPinned = VoicePinToggleButton.IsChecked != false;
        SyncVoiceFlyoutToggleState();
        SaveSettings();
        e.Handled = true;
    }

    private void OnVoiceFlyoutCloseClick(object? sender, RoutedEventArgs e)
    {
        SetVoiceFlyoutOpen(false);
        e.Handled = true;
    }

    private void OnVoiceFlyoutSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CaptureVoiceFlyoutSize();
        SaveSettings();
    }

    private void OnVoiceFlyoutSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CaptureVoiceFlyoutSize();
        SaveSettings();
    }

    private void SetVoiceFlyoutOpen(bool isOpen)
    {
        if (_isVoiceFlyoutOpen == isOpen)
        {
            SyncVoiceFlyoutToggleState();
            return;
        }

        CaptureVoiceFlyoutSize();
        _isVoiceFlyoutOpen = isOpen;
        SyncVoiceFlyoutToggleState();
        ApplyVoiceFlyoutLayout(Bounds.Size);
        SaveSettings();
    }

    private void SyncVoiceFlyoutToggleState()
    {
        _isUpdatingVoiceFlyoutToggleState = true;
        try
        {
            VoiceFlyoutToggleButton.IsChecked = _isVoiceFlyoutOpen;
            VoiceFlyoutToggleButton.Content = _isVoiceFlyoutOpen ? "Hide Voice" : "Voice";
            VoicePinToggleButton.IsChecked = _isVoiceFlyoutPinned;
            VoicePinToggleButton.Content = _isVoiceFlyoutPinned ? "Pinned" : "Pin";
        }
        finally
        {
            _isUpdatingVoiceFlyoutToggleState = false;
        }
    }

    private void ApplyVoiceFlyoutLayout(Size hostSize)
    {
        if (hostSize.Width <= 0 || hostSize.Height <= 0)
            return;

        var isPortrait = hostSize.Height > hostSize.Width;
        _voiceFlyoutIsPortrait = isPortrait;

        ShellGrid.ColumnDefinitions.Clear();
        ShellGrid.RowDefinitions.Clear();

        if (!_isVoiceFlyoutOpen)
        {
            Grid.SetColumn(VoiceFlyoutSplitter, 0);
            Grid.SetRow(VoiceFlyoutSplitter, 0);
            Grid.SetColumn(VoiceFlyoutBorder, 0);
            Grid.SetRow(VoiceFlyoutBorder, 0);
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ShellGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            Grid.SetColumn(MainTabsBorder, 0);
            Grid.SetRow(MainTabsBorder, 0);
            VoiceFlyoutSplitter.IsVisible = false;
            VoiceFlyoutBorder.IsVisible = false;
            return;
        }

        VoiceFlyoutSplitter.IsVisible = true;
        VoiceFlyoutBorder.IsVisible = true;

        if (isPortrait)
        {
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ShellGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            ShellGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            ShellGrid.RowDefinitions.Add(new RowDefinition(CoerceVoiceFlyoutHeight(hostSize), GridUnitType.Pixel));

            Grid.SetColumn(MainTabsBorder, 0);
            Grid.SetRow(MainTabsBorder, 0);
            Grid.SetColumn(VoiceFlyoutSplitter, 0);
            Grid.SetRow(VoiceFlyoutSplitter, 1);
            Grid.SetColumn(VoiceFlyoutBorder, 0);
            Grid.SetRow(VoiceFlyoutBorder, 2);
            VoiceFlyoutSplitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(CoerceVoiceFlyoutWidth(hostSize), GridUnitType.Pixel));
            ShellGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Grid.SetColumn(MainTabsBorder, 0);
            Grid.SetRow(MainTabsBorder, 0);
            Grid.SetColumn(VoiceFlyoutSplitter, 1);
            Grid.SetRow(VoiceFlyoutSplitter, 0);
            Grid.SetColumn(VoiceFlyoutBorder, 2);
            Grid.SetRow(VoiceFlyoutBorder, 0);
            VoiceFlyoutSplitter.ResizeDirection = GridResizeDirection.Columns;
        }
    }

    private double CoerceVoiceFlyoutWidth(Size hostSize)
    {
        var savedWidth = _layoutSettings.VoiceFlyoutLandscapeWidth?.ToGridLength().Value ?? DefaultVoiceFlyoutWidth;
        var maxWidth = Math.Max(MinVoiceFlyoutWidth, hostSize.Width - 220);
        return Math.Clamp(savedWidth, MinVoiceFlyoutWidth, maxWidth);
    }

    private double CoerceVoiceFlyoutHeight(Size hostSize)
    {
        var savedHeight = _layoutSettings.VoiceFlyoutPortraitHeight?.ToGridLength().Value ?? DefaultVoiceFlyoutHeight;
        var maxHeight = Math.Max(MinVoiceFlyoutHeight, hostSize.Height - 160);
        return Math.Clamp(savedHeight, MinVoiceFlyoutHeight, maxHeight);
    }

    private void CaptureVoiceFlyoutSize()
    {
        if (!_isVoiceFlyoutOpen)
            return;

        if (_voiceFlyoutIsPortrait == true)
        {
            var height = VoiceFlyoutBorder.Bounds.Height;
            if (height >= MinVoiceFlyoutHeight)
                _layoutSettings.VoiceFlyoutPortraitHeight = new GridLengthDto(height, GridUnitType.Pixel);
            return;
        }

        if (_voiceFlyoutIsPortrait == false)
        {
            var width = VoiceFlyoutBorder.Bounds.Width;
            if (width >= MinVoiceFlyoutWidth)
                _layoutSettings.VoiceFlyoutLandscapeWidth = new GridLengthDto(width, GridUnitType.Pixel);
        }
    }

    private void LoadSettings()
    {
        _layoutSettings = LayoutSettingsIo.Load() ?? new LayoutSettings();
    }

    private void SaveSettings()
    {
        CaptureVoiceFlyoutSize();
        _layoutSettings.VoiceFlyoutIsOpen = _isVoiceFlyoutOpen;
        _layoutSettings.VoiceFlyoutIsPinned = _isVoiceFlyoutPinned;
        LayoutSettingsIo.Save(_layoutSettings);
    }
}
