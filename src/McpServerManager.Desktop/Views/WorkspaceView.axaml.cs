using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class WorkspaceView : UserControl
{
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private bool _hasAutoLoaded;
    private LayoutSettings _layoutSettings = new();
    private WorkspaceViewModel? _currentViewModel;

    public WorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        ConfigurePromptEditor(GlobalPromptEditor);
        ConfigurePromptEditor(WorkspacePromptEditor);
        ConfigurePromptEditor(WorkspaceStatusPromptEditor);
        ConfigurePromptEditor(WorkspaceImplementPromptEditor);
        ConfigurePromptEditor(WorkspacePlanPromptEditor);
    }

    /// <summary>Inject saved layout settings for splitter persistence.</summary>
    public void ApplySettings(LayoutSettings settings) => _layoutSettings = settings;

    /// <summary>Save current splitter positions back to settings.</summary>
    public void SaveSettings()
    {
        if (_wasPortrait.HasValue)
            SaveCurrentSplitterToSettings(_wasPortrait.Value);
    }

    /// <summary>Called by the host window when the window size changes to handle orientation.</summary>
    public void OnHostSizeChanged(Size newSize)
    {
        if (_isUpdatingLayout || ContentGrid == null) return;

        bool isPortrait = newSize.Height > newSize.Width;
        if (_wasPortrait == isPortrait) return;

        _isUpdatingLayout = true;
        try
        {
            if (_wasPortrait.HasValue)
                SaveCurrentSplitterToSettings(_wasPortrait.Value);
            _wasPortrait = isPortrait;
            UpdateLayoutForOrientation(isPortrait);
        }
        finally
        {
            _isUpdatingLayout = false;
        }
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
        vm.GetWorkspaceStatusPromptEditorText = () => WorkspaceStatusPromptEditor.Text ?? "";
        vm.GetWorkspaceImplementPromptEditorText = () => WorkspaceImplementPromptEditor.Text ?? "";
        vm.GetWorkspacePlanPromptEditorText = () => WorkspacePlanPromptEditor.Text ?? "";
        vm.GetGlobalPromptEditorText = () => GlobalPromptEditor.Text ?? "";
        vm.PropertyChanged += OnViewModelPropertyChanged;
        SetEditorTextIfDifferent(WorkspacePromptEditor, vm.EditorPromptTemplateText);
        SetEditorTextIfDifferent(WorkspaceStatusPromptEditor, vm.EditorStatusPromptText);
        SetEditorTextIfDifferent(WorkspaceImplementPromptEditor, vm.EditorImplementPromptText);
        SetEditorTextIfDifferent(WorkspacePlanPromptEditor, vm.EditorPlanPromptText);
        SetEditorTextIfDifferent(GlobalPromptEditor, vm.GlobalPromptTemplateText);
    }

    private void DetachViewModel(WorkspaceViewModel vm)
    {
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.GetWorkspacePromptEditorText = null;
        vm.GetWorkspaceStatusPromptEditorText = null;
        vm.GetWorkspaceImplementPromptEditorText = null;
        vm.GetWorkspacePlanPromptEditorText = null;
        vm.GetGlobalPromptEditorText = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WorkspaceViewModel vm) return;

        if (e.PropertyName == nameof(WorkspaceViewModel.EditorPromptTemplateText))
        {
            SetEditorTextIfDifferent(WorkspacePromptEditor, vm.EditorPromptTemplateText);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.EditorStatusPromptText))
        {
            SetEditorTextIfDifferent(WorkspaceStatusPromptEditor, vm.EditorStatusPromptText);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.EditorImplementPromptText))
        {
            SetEditorTextIfDifferent(WorkspaceImplementPromptEditor, vm.EditorImplementPromptText);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.EditorPlanPromptText))
        {
            SetEditorTextIfDifferent(WorkspacePlanPromptEditor, vm.EditorPlanPromptText);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.GlobalPromptTemplateText))
        {
            SetEditorTextIfDifferent(GlobalPromptEditor, vm.GlobalPromptTemplateText);
        }
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

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            ContentGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.WorkspaceEditorPortraitListHeight,
                    new GridLength(1, GridUnitType.Star))));
            ContentGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            ContentGrid.RowDefinitions.Add(new RowDefinition(2, GridUnitType.Star));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(WorkspaceSplitter, 1); Grid.SetColumn(WorkspaceSplitter, 0);
            WorkspaceSplitter.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetRow(EditorPanel, 2); Grid.SetColumn(EditorPanel, 0);
        }
        else
        {
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.WorkspaceEditorLandscapeListWidth,
                    new GridLength(1, GridUnitType.Star))));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Star));
            ContentGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Grid.SetColumn(ListPanel, 0); Grid.SetRow(ListPanel, 0);
            Grid.SetColumn(WorkspaceSplitter, 1); Grid.SetRow(WorkspaceSplitter, 0);
            WorkspaceSplitter.ResizeDirection = GridResizeDirection.Columns;
            Grid.SetColumn(EditorPanel, 2); Grid.SetRow(EditorPanel, 0);
        }
    }

    private void SaveCurrentSplitterToSettings(bool wasPortrait)
    {
        if (ContentGrid == null) return;
        if (wasPortrait)
        {
            if (SplitterLayoutPersistence.TryCaptureRowHeight(ContentGrid, 0, out var rowHeight) && rowHeight != null)
                _layoutSettings.WorkspaceEditorPortraitListHeight = rowHeight;
        }
        else
        {
            if (SplitterLayoutPersistence.TryCaptureColumnWidth(ContentGrid, 0, out var columnWidth) && columnWidth != null)
                _layoutSettings.WorkspaceEditorLandscapeListWidth = columnWidth;
        }
    }
}
