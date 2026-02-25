using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("MainWindow");
    private LayoutSettings _layoutSettings = new();
    private ChatWindow? _chatWindow;
    private bool? _chatWindowWasOpenOnClosing;
    private readonly IChatWindowViewModelFactory _chatWindowViewModelFactory = new ChatWindowViewModelFactory();

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
        }

        ContentView.ApplySettings(_layoutSettings);
        TodoView.ApplySettings(_layoutSettings);
        WorkspaceTabView.ApplySettings(_layoutSettings);
        MainTabControl.SelectedIndex = _layoutSettings.SelectedTabIndex;
        MainTabControl.SelectionChanged += OnTabSelectionChanged;

        // Apply initial orientation from current client size
        var initialSize = ClientSize;
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
            await Dispatcher.UIThread.InvokeAsync(() =>
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
            Dispatcher.UIThread.Post(() => Activate(), DispatcherPriority.Input);
        }

        if (_layoutSettings.ChatWindowWasOpen)
            Dispatcher.UIThread.Post(() => ShowChatWindowIfRequested(), DispatcherPriority.Loaded);
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender == MainTabControl)
            _layoutSettings.SelectedTabIndex = MainTabControl.SelectedIndex;
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
        }

        _chatWindowWasOpenOnClosing = _chatWindow != null;
        _chatWindow?.Close();
        _chatWindow = null;

        ContentView.SaveSettings();
        TodoView.SaveSettings();
        WorkspaceTabView.SaveSettings();

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
        ContentView.OnHostSizeChanged(size);
        TodoView.OnHostSizeChanged(size);
        WorkspaceTabView.OnHostSizeChanged(size);
        SaveWindowStateToSettings();
        SaveSettings();
    }

    private void ShowChatWindowIfRequested()
    {
        if (DataContext is not MainWindowViewModel mainVm)
            return;
        if (_chatWindow != null)
        {
            _chatWindow.Activate();
            return;
        }
        var chatVm = _chatWindowViewModelFactory.Create(mainVm.GetLogContextForAgent);
        mainVm.SetContextConsumer(s => chatVm.NotifyContextChanged(s));
        mainVm.SetModelConsumer(m => { if (_chatWindow?.DataContext is ChatWindowViewModel cvm) cvm.SelectedModel = m; });
        _chatWindow = new ChatWindow { DataContext = chatVm };
        _chatWindow.Closed += (_, _) =>
        {
            mainVm.SetContextConsumer(null);
            mainVm.SetModelConsumer(null);
            _chatWindow = null;
            PersistChatWindowClosed();
        };
        _chatWindow.Show();
        chatVm.NotifyContextChanged(mainVm.GetLogContextForAgent());
        mainVm.ApplyModelForCurrentSelection();
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
}
