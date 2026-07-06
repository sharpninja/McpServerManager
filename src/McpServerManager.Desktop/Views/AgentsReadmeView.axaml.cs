using System;
using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class AgentsReadmeView : UserControl
{
    private MainWindowViewModel? _currentViewModel;

    public AgentsReadmeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ConfigureEditor(AgentsReadmeEditor);
    }

    protected void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(_currentViewModel, DataContext))
        {
            if (_currentViewModel != null)
                _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _currentViewModel = DataContext as MainWindowViewModel;

            if (_currentViewModel != null)
                _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        SetEditorTextIfDifferent(AgentsReadmeEditor, _currentViewModel?.AgentsReadmeContent);
    }

    protected void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
            return;

        if (e.PropertyName == nameof(MainWindowViewModel.AgentsReadmeContent))
            SetEditorTextIfDifferent(AgentsReadmeEditor, vm.AgentsReadmeContent);
    }

    protected static void ConfigureEditor(TextEditor editor)
    {
        editor.IsReadOnly = true;
        editor.WordWrap = false;
        editor.Text = "";
    }

    protected static void SetEditorTextIfDifferent(TextEditor editor, string? text)
    {
        var next = text ?? "";
        if (!string.Equals(editor.Text ?? "", next, StringComparison.Ordinal))
            editor.Text = next;
    }
}
