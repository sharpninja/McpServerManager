using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class WorkspaceView : UserControl
{
    private bool _hasAutoLoaded;

    public WorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await TryAutoLoadAsync();
    }

    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        await TryAutoLoadAsync();
    }

    private async Task TryAutoLoadAsync()
    {
        if (_hasAutoLoaded) return;
        if (DataContext is not WorkspaceViewModel vm) return;
        _hasAutoLoaded = true;
        await vm.LoadWorkspacesCommand.ExecuteAsync(null);
    }
}
