using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using UiDispatcherHost = McpServer.UI.Core.Services.UiDispatcherHost;

namespace McpServerManager.Desktop.Views;

public partial class MainWindow : Window
{
    private const double MinVoiceFlyoutWidth = 320;
    private const double MinVoiceFlyoutHeight = 240;
    private const double DefaultVoiceFlyoutWidth = 420;
    private const double DefaultVoiceFlyoutHeight = 300;

    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("MainWindow");
    private LayoutSettings _layoutSettings = new();
    private ChatWindow? _chatWindow;
    private ChatWindowViewModelSession? _chatWindowSession;
    private bool? _chatWindowWasOpenOnClosing;
    private bool _isVoiceFlyoutOpen;
    private bool _isVoiceFlyoutPinned = true;
    private bool? _voiceFlyoutIsPortrait;
    private bool _isUpdatingVoiceFlyoutToggleState;

    public IChatWindowViewModelFactory? ChatWindowViewModelFactory { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        LoadSettings();
        ApplyWindowSettings();

        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        Opened += OnWindowOpened;
        this.GetObservable(WindowStateProperty).Subscribe(new WindowStateObserver(this));
    }

    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly MainWindow _window;
        public WindowStateObserver(MainWindow window) => _window = window;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value) { _window.SaveWindowStateToSettings(); _window.SaveSettings(); }
    }

    private void ApplyWindowSettings()
    {
        try
        {
            if (_layoutSettings.WindowWidth < 100) _layoutSettings.WindowWidth = 1000;
            if (_layoutSettings.WindowHeight < 100) _layoutSettings.WindowHeight = 800;

            Width = _layoutSettings.WindowWidth;
            Height = _layoutSettings.WindowHeight;

            int x = (int)_layoutSettings.WindowX;
            int y = (int)_layoutSettings.WindowY;
            bool hasValidSavedPosition = x >= -50 && x <= 10000 && y >= -50 && y <= 10000 && (x != 0 || y != 0);
            if (hasValidSavedPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(x, y);
            }
            else
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

            WindowState = _layoutSettings.WindowState == WindowState.Minimized
                ? WindowState.Normal
                : _layoutSettings.WindowState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error applying window settings: {Message}", ex.Message);
        }
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.InitializeAfterWindowShown();
            vm.OpenChatWindowRequested += OnOpenChatWindowRequested;
            vm.TodoViewModel.OpenAiChatRequested += OnTodoOpenAiChatRequested;
            vm.LogoutRequested += OnLogoutRequested;
        }

        ContentView.ApplySettings(_layoutSettings);
        TodoView.ApplySettings(_layoutSettings);
        WorkspaceTabView.ApplySettings(_layoutSettings);
        _isVoiceFlyoutOpen = _layoutSettings.VoiceFlyoutIsOpen;
        _isVoiceFlyoutPinned = _layoutSettings.VoiceFlyoutIsPinned;
        SyncVoiceFlyoutToggleState();
        MainTabControl.SelectedIndex = Math.Clamp(_layoutSettings.SelectedTabIndex, 0, Math.Max(0, MainTabControl.ItemCount - 1));
        MainTabControl.SelectionChanged += OnTabSelectionChanged;

        // Apply initial orientation from current client size
        var initialSize = ClientSize;
        ApplyVoiceFlyoutLayout(initialSize);
        ContentView.OnHostSizeChanged(initialSize);
        TodoView.OnHostSizeChanged(initialSize);
        WorkspaceTabView.OnHostSizeChanged(initialSize);

        int sx = (int)_layoutSettings.WindowX;
        int sy = (int)_layoutSettings.WindowY;
        bool validSaved = sx >= -50 && sx <= 10000 && sy >= -50 && sy <= 10000 && (sx != 0 || sy != 0);
        if (validSaved && WindowState == WindowState.Normal)
        {
            Position = new PixelPoint(sx, sy);
            await Task.Delay(100);
            await DispatchToUiAsync(() =>
            {
                if (WindowState == WindowState.Normal)
                    Position = new PixelPoint(sx, sy);
            });
        }

        var pos = Position;
        if (pos.X < -100 || pos.Y < -100 || pos.X > 10000 || pos.Y > 10000)
            Position = new PixelPoint(50, 50);

        Activate();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await Task.Delay(150);
            DispatchToUi(Activate);
        }

        if (_layoutSettings.ChatWindowWasOpen)
            DispatchToUi(ShowChatWindowIfRequested);
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender == MainTabControl)
        {
            _layoutSettings.SelectedTabIndex = MainTabControl.SelectedIndex;
            if (_isVoiceFlyoutOpen && !_isVoiceFlyoutPinned)
                SetVoiceFlyoutOpen(false);
        }
    }

    private void OnOpenChatWindowRequested(object? sender, EventArgs e)
    {
        ShowChatWindowIfRequested();
    }

    private void OnTodoOpenAiChatRequested(object? sender, EventArgs e)
    {
        ShowChatWindowIfRequested();
        // Inject todo context into the chat's context provider
        if (DataContext is MainWindowViewModel vm && _chatWindow?.DataContext is ChatWindowViewModel chatVm)
        {
            var todoContext = vm.TodoViewModel.GetTodoContextForAgent();
            chatVm.NotifyContextChanged(todoContext);
        }
    }

    private void LoadSettings()
    {
        try
        {
            var settings = LayoutSettingsIo.Load();
            if (settings != null)
                _layoutSettings = settings;
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            CaptureVoiceFlyoutSize();
            var toSave = LayoutSettingsIo.Load() ?? new LayoutSettings();
            toSave.LandscapeLeftColWidth = _layoutSettings.LandscapeLeftColWidth;
            toSave.LandscapeHistoryRowHeight = _layoutSettings.LandscapeHistoryRowHeight;
            toSave.PortraitTreeRowHeight = _layoutSettings.PortraitTreeRowHeight;
            toSave.PortraitViewerRowHeight = _layoutSettings.PortraitViewerRowHeight;
            toSave.PortraitHistoryRowHeight = _layoutSettings.PortraitHistoryRowHeight;
            toSave.JsonViewerSearchIndexRowHeight = _layoutSettings.JsonViewerSearchIndexRowHeight;
            toSave.JsonViewerTreeRowHeight = _layoutSettings.JsonViewerTreeRowHeight;
            toSave.WindowWidth = _layoutSettings.WindowWidth;
            toSave.WindowHeight = _layoutSettings.WindowHeight;
            toSave.WindowX = _layoutSettings.WindowX;
            toSave.WindowY = _layoutSettings.WindowY;
            toSave.WindowState = _layoutSettings.WindowState;
            toSave.ChatWindowWasOpen = _chatWindowWasOpenOnClosing ?? (_chatWindow != null);
            toSave.SelectedTabIndex = _layoutSettings.SelectedTabIndex;
            toSave.TodoEditorLandscapeListWidth = _layoutSettings.TodoEditorLandscapeListWidth;
            toSave.TodoEditorPortraitListHeight = _layoutSettings.TodoEditorPortraitListHeight;
            toSave.WorkspaceEditorLandscapeListWidth = _layoutSettings.WorkspaceEditorLandscapeListWidth;
            toSave.WorkspaceEditorPortraitListHeight = _layoutSettings.WorkspaceEditorPortraitListHeight;
            toSave.VoiceFlyoutLandscapeWidth = _layoutSettings.VoiceFlyoutLandscapeWidth;
            toSave.VoiceFlyoutPortraitHeight = _layoutSettings.VoiceFlyoutPortraitHeight;
            toSave.VoiceFlyoutIsOpen = _isVoiceFlyoutOpen;
            toSave.VoiceFlyoutIsPinned = _isVoiceFlyoutPinned;
            _chatWindowWasOpenOnClosing = null;
            LayoutSettingsIo.Save(toSave);
        }
        catch { }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenChatWindowRequested -= OnOpenChatWindowRequested;
            vm.TodoViewModel.OpenAiChatRequested -= OnTodoOpenAiChatRequested;
            vm.LogoutRequested -= OnLogoutRequested;
        }

        _chatWindowWasOpenOnClosing = _chatWindow != null;
        _chatWindow?.Close();
        _chatWindow = null;
        _chatWindowSession?.Dispose();
        _chatWindowSession = null;

        ContentView.SaveSettings();
        TodoView.SaveSettings();
        WorkspaceTabView.SaveSettings();
        CaptureVoiceFlyoutSize();

        if (WindowState == WindowState.Normal)
        {
            _layoutSettings.WindowWidth = Width;
            _layoutSettings.WindowHeight = Height;
            _layoutSettings.WindowX = Position.X;
            _layoutSettings.WindowY = Position.Y;
        }
        _layoutSettings.WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        SaveSettings();
    }

    private void SaveWindowStateToSettings()
    {
        if (WindowState == WindowState.Minimized)
            return;
        _layoutSettings.WindowState = WindowState;
        if (WindowState == WindowState.Normal)
        {
            _layoutSettings.WindowWidth = Width;
            _layoutSettings.WindowHeight = Height;
            _layoutSettings.WindowX = Position.X;
            _layoutSettings.WindowY = Position.Y;
        }
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Minimized) return;
        var size = ClientSize;
        CaptureVoiceFlyoutSize();
        ApplyVoiceFlyoutLayout(size);
        ContentView.OnHostSizeChanged(size);
        TodoView.OnHostSizeChanged(size);
        WorkspaceTabView.OnHostSizeChanged(size);
        SaveWindowStateToSettings();
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

    private static void DispatchToUi(Action action) => UiDispatcherHost.Post(action);

    private static Task DispatchToUiAsync(Action action)
        => UiDispatcherHost.InvokeAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });

    private void ShowChatWindowIfRequested()
    {
        if (DataContext is not MainWindowViewModel mainVm)
            return;
        if (ChatWindowViewModelFactory is null)
        {
            _logger.LogWarning("Chat window requested before a chat factory was configured.");
            return;
        }
        if (_chatWindow != null)
        {
            _chatWindow.Activate();
            return;
        }
        var chatSession = ChatWindowViewModelFactory.Create(mainVm.GetLogContextForAgent);
        var chatVm = chatSession.ViewModel;
        mainVm.SetContextConsumer(s => chatVm.NotifyContextChanged(s));
        mainVm.SetModelConsumer(m => { if (_chatWindow?.DataContext is ChatWindowViewModel cvm) cvm.SelectedModel = m; });
        _chatWindowSession = chatSession;
        _chatWindow = new ChatWindow { DataContext = chatVm };
        _chatWindow.Closed += (_, _) =>
        {
            mainVm.SetContextConsumer(null);
            mainVm.SetModelConsumer(null);
            _chatWindow = null;
            _chatWindowSession?.Dispose();
            _chatWindowSession = null;
            PersistChatWindowClosed();
        };
        _chatWindow.Show();
        chatVm.NotifyContextChanged(mainVm.GetLogContextForAgent());
        mainVm.ApplyModelForCurrentSelection();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        _logger.LogInformation("Logout requested; closing application");
        Close();
    }

    private void PersistChatWindowClosed()
    {
        try
        {
            var s = LayoutSettingsIo.Load() ?? new LayoutSettings();
            s.ChatWindowWasOpen = false;
            LayoutSettingsIo.Save(s);
        }
        catch { }
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
        ApplyVoiceFlyoutLayout(ClientSize);
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
        if (ShellGrid == null)
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
        var maxWidth = Math.Max(MinVoiceFlyoutWidth, hostSize.Width - 240);
        return Math.Clamp(savedWidth, MinVoiceFlyoutWidth, maxWidth);
    }

    private double CoerceVoiceFlyoutHeight(Size hostSize)
    {
        var savedHeight = _layoutSettings.VoiceFlyoutPortraitHeight?.ToGridLength().Value ?? DefaultVoiceFlyoutHeight;
        var maxHeight = Math.Max(MinVoiceFlyoutHeight, hostSize.Height - 180);
        return Math.Clamp(savedHeight, MinVoiceFlyoutHeight, maxHeight);
    }

    private void CaptureVoiceFlyoutSize()
    {
        if (!_isVoiceFlyoutOpen || VoiceFlyoutBorder == null)
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
}
