using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class TodoListView : UserControl
{
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private bool _hasAutoLoaded;
    private LayoutSettings _layoutSettings = new();
    private readonly List<ListBox> _groupListBoxes = new();

    public TodoListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Editor.FontFamily = new Avalonia.Media.FontFamily("Cascadia Code,Consolas,Menlo,monospace");
        Editor.WordWrap = true;
        Editor.Text = "";
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

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // No auto-load — workspace-change event triggers the initial load.
        _hasAutoLoaded = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TodoListViewModel vm)
        {
            vm.GetEditorText = () => Editor.Text;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TodoListViewModel vm) return;
        if (e.PropertyName == nameof(TodoListViewModel.EditorText))
            Editor.Text = vm.EditorText;
        else if (e.PropertyName == nameof(TodoListViewModel.EditorFontSize))
            Editor.FontSize = vm.EditorFontSize;
        else if (e.PropertyName == nameof(TodoListViewModel.SelectedEditorTab))
            SyncTabContent(vm);
    }

    private void SyncTabContent(TodoListViewModel vm)
    {
        var tab = vm.SelectedEditorTab;
        if (tab is null)
        {
            EditorContent.IsVisible = false;
            MarkdownViewer.IsVisible = false;
            return;
        }

        if (tab.IsMarkdown)
        {
            EditorContent.IsVisible = false;
            MarkdownText.Text = tab.Content;
            MarkdownViewer.IsVisible = true;
            // Watch for streaming updates
            tab.PropertyChanged -= OnTabContentChanged;
            tab.PropertyChanged += OnTabContentChanged;
        }
        else
        {
            MarkdownViewer.IsVisible = false;
            EditorContent.IsVisible = true;
        }
    }

    private void OnTabContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorTab.Content) && sender is EditorTab tab && tab.IsMarkdown)
            MarkdownText.Text = tab.Content;
    }

    /// <summary>Called from XAML when a per-group ListBox selection changes. Ensures single selection across all groups and auto-opens the todo.</summary>
    private void OnGroupListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox activeListBox) return;
        if (activeListBox.SelectedItem == null) return;

        // Deselect all other group ListBoxes
        foreach (var lb in _groupListBoxes)
        {
            if (!ReferenceEquals(lb, activeListBox))
                lb.SelectedItem = null;
        }

        // Propagate selection to ViewModel and auto-open
        if (DataContext is TodoListViewModel vm && activeListBox.SelectedItem is TodoListEntry entry)
        {
            vm.SelectedEntry = entry;
            if (vm.OpenSelectedTodoCommand.CanExecute(null))
                vm.OpenSelectedTodoCommand.Execute(null);
        }
    }

    /// <summary>Track ListBox instances as they are created by the ItemsControl template.</summary>
    private void OnGroupListBoxLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ListBox lb && !_groupListBoxes.Contains(lb))
            _groupListBoxes.Add(lb);
    }

    /// <summary>Remove tracked ListBox instances when unloaded (group removed by filter).</summary>
    private void OnGroupListBoxUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ListBox lb)
            _groupListBoxes.Remove(lb);
    }

    private void OnEditorCut(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Cut();

    private async void OnEditorCopy(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedText = Editor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        try
        {
            await topLevel.Clipboard.SetTextAsync(selectedText);
        }
        catch
        {
            // Avoid UI lockups/fault propagation from platform clipboard failures.
        }
    }

    private void OnEditorPaste(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Paste();

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            // Stacked: list on top (1/3), splitter, editor below (2/3)
            ContentGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.TodoEditorPortraitListHeight,
                    new GridLength(1, GridUnitType.Star))));
            ContentGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            ContentGrid.RowDefinitions.Add(new RowDefinition(2, GridUnitType.Star));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(TodoSplitter, 1); Grid.SetColumn(TodoSplitter, 0);
            TodoSplitter.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetRow(EditorPanel, 2); Grid.SetColumn(EditorPanel, 0);
        }
        else
        {
            // Side-by-side: list left (1/3), splitter, editor right (2/3)
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.TodoEditorLandscapeListWidth,
                    new GridLength(1, GridUnitType.Star))));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Star));
            ContentGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Grid.SetColumn(ListPanel, 0); Grid.SetRow(ListPanel, 0);
            Grid.SetColumn(TodoSplitter, 1); Grid.SetRow(TodoSplitter, 0);
            TodoSplitter.ResizeDirection = GridResizeDirection.Columns;
            Grid.SetColumn(EditorPanel, 2); Grid.SetRow(EditorPanel, 0);
        }
    }

    private void SaveCurrentSplitterToSettings(bool wasPortrait)
    {
        if (ContentGrid == null) return;
        if (wasPortrait)
        {
            if (SplitterLayoutPersistence.TryCaptureRowHeight(ContentGrid, 0, out var rowHeight) && rowHeight != null)
                _layoutSettings.TodoEditorPortraitListHeight = rowHeight;
        }
        else
        {
            if (SplitterLayoutPersistence.TryCaptureColumnWidth(ContentGrid, 0, out var columnWidth) && columnWidth != null)
                _layoutSettings.TodoEditorLandscapeListWidth = columnWidth;
        }
    }
}
