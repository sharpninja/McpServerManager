using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using RequestTracker.Core.Models;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Android.Views;

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
        Editor.FontFamily = new Avalonia.Media.FontFamily("avares://RequestTracker.Android/Assets/Fonts/FiraCode-Regular.ttf#Fira Code");
        Editor.WordWrap = true;
    }

    public void ApplySettings(LayoutSettings settings) => _layoutSettings = settings;

    public void SaveSettings()
    {
        if (_wasPortrait.HasValue)
            SaveCurrentSplitterToSettings(_wasPortrait.Value);
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _hasAutoLoaded = true;
        if (DataContext is TodoListViewModel vm && vm.GroupedItems.Count == 0 && !vm.IsLoading)
            await vm.LoadTodosCommand.ExecuteAsync(null);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TodoListViewModel vm)
        {
            vm.GetEditorText = () => Editor.Text;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Auto-load if DataContext arrived after Loaded event
            if (_hasAutoLoaded && vm.GroupedItems.Count == 0 && !vm.IsLoading)
                _ = vm.LoadTodosCommand.ExecuteAsync(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TodoListViewModel vm) return;
        if (e.PropertyName == nameof(TodoListViewModel.EditorText))
            Editor.Text = vm.EditorText;
        else if (e.PropertyName == nameof(TodoListViewModel.EditorFontSize))
            Editor.FontSize = vm.EditorFontSize;
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
            ContentGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.TodoEditorPortraitListHeight.ToGridLength()));
            ContentGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            ContentGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(TodoSplitter, 1); Grid.SetColumn(TodoSplitter, 0);
            TodoSplitter.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetRow(EditorPanel, 2); Grid.SetColumn(EditorPanel, 0);
        }
        else
        {
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(_layoutSettings.TodoEditorLandscapeListWidth.ToGridLength()));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
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
