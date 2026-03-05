using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

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
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    public void ApplySettings(LayoutSettings settings) => _layoutSettings = settings;

    public void SaveSettings()
    {
        if (_wasPortrait.HasValue)
            SaveCurrentSplitterToSettings(_wasPortrait.Value);
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
            Editor.IsVisible = false;
            MarkdownViewer.IsVisible = false;
            return;
        }

        if (tab.IsMarkdown)
        {
            Editor.IsVisible = false;
            MarkdownText.Text = tab.Content;
            MarkdownViewer.IsVisible = true;
            tab.PropertyChanged -= OnTabContentChanged;
            tab.PropertyChanged += OnTabContentChanged;
        }
        else
        {
            MarkdownViewer.IsVisible = false;
            Editor.IsVisible = true;
        }
    }

    private void OnTabContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorTab.Content) && sender is EditorTab tab && tab.IsMarkdown)
            MarkdownText.Text = tab.Content;
    }

    private void OnGroupListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox activeListBox) return;
        if (activeListBox.SelectedItem == null) return;

        foreach (var lb in _groupListBoxes)
        {
            if (!ReferenceEquals(lb, activeListBox))
                lb.SelectedItem = null;
        }

        if (DataContext is TodoListViewModel vm && activeListBox.SelectedItem is TodoListEntry entry)
        {
            vm.SelectedEntry = entry;
            if (vm.OpenSelectedTodoCommand.CanExecute(null))
                vm.OpenSelectedTodoCommand.Execute(null);
        }
    }

    private void OnGroupListBoxLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ListBox lb && !_groupListBoxes.Contains(lb))
            _groupListBoxes.Add(lb);
    }

    private void OnGroupListBoxUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ListBox lb)
            _groupListBoxes.Remove(lb);
    }

    private void OnEditorCut(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Cut();
    private void OnEditorCopy(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Copy();
    private void OnEditorPaste(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Paste();

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isUpdatingLayout || ContentGrid == null) return;

        bool isPortrait = e.NewSize.Height > e.NewSize.Width;
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

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            // Stacked: list on top (1/3), splitter, editor below (2/3)
            ContentGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.TodoEditorPortraitListHeight.ToGridLength()));
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
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(_layoutSettings.TodoEditorLandscapeListWidth.ToGridLength()));
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
            if (ContentGrid.RowDefinitions.Count >= 1)
                _layoutSettings.TodoEditorPortraitListHeight = GridLengthDto.FromGridLength(ContentGrid.RowDefinitions[0].Height);
        }
        else
        {
            if (ContentGrid.ColumnDefinitions.Count >= 1)
                _layoutSettings.TodoEditorLandscapeListWidth = GridLengthDto.FromGridLength(ContentGrid.ColumnDefinitions[0].Width);
        }
    }
}
