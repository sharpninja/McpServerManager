using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using RequestTracker.Core.Models;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Desktop.Views;

public partial class MainWindow : Window
{
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private LayoutSettings _layoutSettings = new();
    private ChatWindow? _chatWindow;
    private bool? _chatWindowWasOpenOnClosing;

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
            Console.Error.WriteLine($"Error applying window settings: {ex.Message}");
        }
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.InitializeAfterWindowShown();
            vm.OpenChatWindowRequested += OnOpenChatWindowRequested;
        }

        ApplyJsonViewerSplitterSettings();

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

    private void OnOpenChatWindowRequested(object? sender, EventArgs e)
    {
        ShowChatWindowIfRequested();
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
            _chatWindowWasOpenOnClosing = null;
            LayoutSettingsIo.Save(toSave);
        }
        catch { }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.OpenChatWindowRequested -= OnOpenChatWindowRequested;

        _chatWindowWasOpenOnClosing = _chatWindow != null;
        _chatWindow?.Close();
        _chatWindow = null;

        if (_wasPortrait.HasValue)
            SaveCurrentLayoutToSettings(_wasPortrait.Value);
        SaveJsonViewerSplitterSettings();

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

    private void ApplyJsonViewerSplitterSettings()
    {
        try
        {
            if (JsonViewerGrid?.RowDefinitions == null || JsonViewerGrid.RowDefinitions.Count < 5)
                return;
            var searchIndexLength = _layoutSettings.JsonViewerSearchIndexRowHeight.ToGridLength();
            if (searchIndexLength.GridUnitType == GridUnitType.Star)
                searchIndexLength = new GridLength(200, GridUnitType.Pixel);
            JsonViewerGrid.RowDefinitions[2].Height = searchIndexLength;
            JsonViewerGrid.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error applying JSON viewer splitter: {ex.Message}");
        }
    }

    private void SaveJsonViewerSplitterSettings()
    {
        try
        {
            if (JsonViewerGrid?.RowDefinitions == null || JsonViewerGrid.RowDefinitions.Count < 5)
                return;
            _layoutSettings.JsonViewerSearchIndexRowHeight = GridLengthDto.FromGridLength(JsonViewerGrid.RowDefinitions[2].Height);
            _layoutSettings.JsonViewerTreeRowHeight = GridLengthDto.FromGridLength(JsonViewerGrid.RowDefinitions[4].Height);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving JSON viewer splitter: {ex.Message}");
        }
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
        if (_isUpdatingLayout) return;
        if (WindowState == WindowState.Minimized) return;

        bool isPortrait = e.NewSize.Height > e.NewSize.Width;
        if (_wasPortrait == isPortrait) return;

        _isUpdatingLayout = true;
        try
        {
            if (_wasPortrait.HasValue)
                SaveCurrentLayoutToSettings(_wasPortrait.Value);
            _wasPortrait = isPortrait;
            UpdateLayoutForOrientation(isPortrait);
            SaveWindowStateToSettings();
            SaveSettings();
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    private void SaveCurrentLayoutToSettings(bool isPortrait)
    {
        if (MainGrid == null) return;
        if (isPortrait)
        {
            if (MainGrid.RowDefinitions.Count >= 6)
            {
                _layoutSettings.PortraitTreeRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[0].Height);
                _layoutSettings.PortraitViewerRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[2].Height);
                _layoutSettings.PortraitHistoryRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[4].Height);
            }
        }
        else
        {
            if (MainGrid.ColumnDefinitions.Count >= 1 && MainGrid.RowDefinitions.Count >= 4)
            {
                _layoutSettings.LandscapeLeftColWidth = GridLengthDto.FromGridLength(MainGrid.ColumnDefinitions[0].Width);
                _layoutSettings.LandscapeHistoryRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[2].Height);
            }
        }
    }

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        if (MainGrid == null) return;
        MainGrid.ColumnDefinitions.Clear();
        MainGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitTreeRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitViewerRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitHistoryRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(ViewerPanel, 0); Grid.SetRow(ViewerPanel, 2); Grid.SetRowSpan(ViewerPanel, 1);
            Grid.SetColumn(Splitter2, 0); Grid.SetRow(Splitter2, 3); Grid.SetRowSpan(Splitter2, 1);
            Splitter2.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 4);
            Grid.SetColumn(StatusBarBorder, 0); Grid.SetColumnSpan(StatusBarBorder, 1); Grid.SetRow(StatusBarBorder, 5);
        }
        else
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(_layoutSettings.LandscapeLeftColWidth.ToGridLength()));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.LandscapeHistoryRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 2);
            Grid.SetColumn(Splitter2, 1); Grid.SetRow(Splitter2, 0); Grid.SetRowSpan(Splitter2, 3);
            Splitter2.ResizeDirection = GridResizeDirection.Columns;
            Grid.SetColumn(ViewerPanel, 2); Grid.SetRow(ViewerPanel, 0); Grid.SetRowSpan(ViewerPanel, 3);
            Grid.SetColumn(StatusBarBorder, 0); Grid.SetColumnSpan(StatusBarBorder, 3); Grid.SetRow(StatusBarBorder, 3);
        }
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
        var agentService = new RequestTracker.Core.Services.OllamaLogAgentService();
        var configModel = RequestTracker.Core.Models.AgentConfigIo.GetModelFromConfig();
        var chatVm = new ChatWindowViewModel(agentService, mainVm.GetLogContextForAgent, configModel, model => RequestTracker.Core.Models.AgentConfigIo.SetModelInConfig(model));
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
