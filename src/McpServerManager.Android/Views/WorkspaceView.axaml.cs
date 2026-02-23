using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaEdit;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class WorkspaceView : UserControl
{
    private bool _hasAutoLoaded;
    private WorkspaceViewModel? _currentViewModel;

    public WorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        ConfigurePromptEditor(WorkspacePromptEditor);
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await TryAutoLoadAsync();
    }

    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(_currentViewModel, DataContext))
        {
            if (_currentViewModel != null)
                DetachViewModel(_currentViewModel);

            _currentViewModel = DataContext as WorkspaceViewModel;
            if (_currentViewModel != null)
                AttachViewModel(_currentViewModel);
        }

        await TryAutoLoadAsync();
    }

    private async Task TryAutoLoadAsync()
    {
        if (_hasAutoLoaded) return;
        if (DataContext is not WorkspaceViewModel vm) return;
        _hasAutoLoaded = true;
        await vm.LoadWorkspacesCommand.ExecuteAsync(null);
    }

    private void AttachViewModel(WorkspaceViewModel vm)
    {
        vm.GetWorkspacePromptEditorText = () => WorkspacePromptEditor.Text ?? "";
        vm.PropertyChanged += OnViewModelPropertyChanged;
        SetEditorTextIfDifferent(WorkspacePromptEditor, vm.EditorPromptTemplateText);
    }

    private void DetachViewModel(WorkspaceViewModel vm)
    {
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.GetWorkspacePromptEditorText = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WorkspaceViewModel vm) return;
        if (e.PropertyName == nameof(WorkspaceViewModel.EditorPromptTemplateText))
            SetEditorTextIfDifferent(WorkspacePromptEditor, vm.EditorPromptTemplateText);
    }

    private static void ConfigurePromptEditor(TextEditor editor)
    {
        editor.FontFamily = new Avalonia.Media.FontFamily("Cascadia Code,Consolas,Menlo,monospace");
        editor.WordWrap = true;
        editor.Text = "";
    }

    private static void SetEditorTextIfDifferent(TextEditor editor, string? text)
    {
        var next = text ?? "";
        if (!string.Equals(editor.Text ?? "", next, StringComparison.Ordinal))
            editor.Text = next;
    }
}
