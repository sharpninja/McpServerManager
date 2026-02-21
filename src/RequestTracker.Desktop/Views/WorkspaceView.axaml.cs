using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using RequestTracker.Core.Models;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Desktop.Views;

public partial class WorkspaceView : UserControl
{
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private bool _hasAutoLoaded;
    private LayoutSettings _layoutSettings = new();

    public WorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
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
        await TryAutoLoadAsync();
    }

    private async Task TryAutoLoadAsync()
    {
        if (_hasAutoLoaded) return;
        if (DataContext is not WorkspaceViewModel vm) return;
        _hasAutoLoaded = true;
        await vm.LoadWorkspacesCommand.ExecuteAsync(null);
    }

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            ContentGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.WorkspaceEditorPortraitListHeight.ToGridLength()));
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
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(_layoutSettings.WorkspaceEditorLandscapeListWidth.ToGridLength()));
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
            if (ContentGrid.RowDefinitions.Count >= 1)
                _layoutSettings.WorkspaceEditorPortraitListHeight =
                    GridLengthDto.FromGridLength(ContentGrid.RowDefinitions[0].Height);
        }
        else
        {
            if (ContentGrid.ColumnDefinitions.Count >= 1)
                _layoutSettings.WorkspaceEditorLandscapeListWidth =
                    GridLengthDto.FromGridLength(ContentGrid.ColumnDefinitions[0].Width);
        }
    }
}
