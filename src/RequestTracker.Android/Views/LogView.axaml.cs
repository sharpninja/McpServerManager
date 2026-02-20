using Avalonia.Controls;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Android.Views;

public partial class LogView : UserControl
{
    private bool _wasPausedBeforeContextMenu;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is LogViewModel vm)
        {
            vm.NewEntryAdded += OnNewEntryAdded;
        }

        if (LogListBox.ContextMenu is { } menu)
        {
            menu.Opening += OnContextMenuOpening;
            menu.Closing += OnContextMenuClosing;
        }
    }

    private void OnContextMenuOpening(object? sender, System.EventArgs e)
    {
        if (DataContext is LogViewModel vm)
        {
            _wasPausedBeforeContextMenu = vm.IsPaused;
            vm.IsPaused = true;
        }
    }

    private void OnContextMenuClosing(object? sender, System.EventArgs e)
    {
        if (DataContext is LogViewModel vm && !_wasPausedBeforeContextMenu)
        {
            vm.IsPaused = false;
        }
    }

    private void OnNewEntryAdded()
    {
        if (DataContext is LogViewModel vm && vm.LogEntries.Count > 0)
        {
            LogListBox.ScrollIntoView(vm.LogEntries[^1]);
        }
    }
}
