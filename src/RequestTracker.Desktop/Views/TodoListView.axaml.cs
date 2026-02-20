using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RequestTracker.Core.Models;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Desktop.Views;

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
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
        if (_hasAutoLoaded) return;
        _hasAutoLoaded = true;
        if (DataContext is TodoListViewModel vm)
            await vm.LoadTodosCommand.ExecuteAsync(null);
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
    }

    private void OnListItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is TodoListViewModel vm && vm.OpenSelectedTodoCommand.CanExecute(null))
            vm.OpenSelectedTodoCommand.Execute(null);
    }

    /// <summary>Called from XAML when a per-group ListBox selection changes. Ensures single selection across all groups.</summary>
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

        // Propagate selection to ViewModel
        if (DataContext is TodoListViewModel vm && activeListBox.SelectedItem is TodoListEntry entry)
            vm.SelectedEntry = entry;
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
    private void OnEditorCopy(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Copy();
    private void OnEditorPaste(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Editor.Paste();

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            // Stacked: list on top, splitter, editor below
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
            // Side-by-side: list left, splitter, editor right
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
