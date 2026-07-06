using Avalonia.Controls;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class LogView : UserControl
{
    private bool _wasPausedBeforeContextMenu;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected void OnDataContextChanged(object? sender, System.EventArgs e)
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

    protected void OnContextMenuOpening(object? sender, System.EventArgs e)
    {
        if (DataContext is LogViewModel vm)
        {
            _wasPausedBeforeContextMenu = vm.IsPaused;
            vm.IsPaused = true;
        }
    }

    protected void OnContextMenuClosing(object? sender, System.EventArgs e)
    {
        if (DataContext is LogViewModel vm && !_wasPausedBeforeContextMenu)
        {
            vm.IsPaused = false;
        }
    }

    protected void OnNewEntryAdded()
    {
        if (DataContext is LogViewModel vm && vm.LogEntries.Count > 0)
        {
            LogListBox.ScrollIntoView(vm.LogEntries[^1]);
        }
    }
}
