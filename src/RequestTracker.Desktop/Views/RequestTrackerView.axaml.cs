using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RequestTracker.Core.Models;

namespace RequestTracker.Desktop.Views;

public partial class RequestTrackerView : UserControl
{
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private LayoutSettings _layoutSettings = new();

    public RequestTrackerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Inject saved layout settings and apply initial state.</summary>
    public void ApplySettings(LayoutSettings settings)
    {
        _layoutSettings = settings;
        ApplyJsonViewerSplitterSettings();
    }

    /// <summary>Save current grid layout and splitter positions back to settings object.</summary>
    public void SaveSettings()
    {
        if (_wasPortrait.HasValue)
            SaveCurrentLayoutToSettings(_wasPortrait.Value);
        SaveJsonViewerSplitterSettings();
    }

    /// <summary>Called by the host window when the window size changes to handle orientation.</summary>
    public void OnHostSizeChanged(Size newSize)
    {
        if (_isUpdatingLayout) return;

        bool isPortrait = newSize.Height > newSize.Width;
        if (_wasPortrait == isPortrait) return;

        _isUpdatingLayout = true;
        try
        {
            if (_wasPortrait.HasValue)
                SaveCurrentLayoutToSettings(_wasPortrait.Value);
            _wasPortrait = isPortrait;
            UpdateLayoutForOrientation(isPortrait);
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    private void ApplyJsonViewerSplitterSettings()
    {
        try
        {
            if (JsonViewerGrid?.RowDefinitions == null || JsonViewerGrid.RowDefinitions.Count < 5)
                return;
            var searchIndexLength = _layoutSettings.JsonViewerSearchIndexRowHeight.ToGridLength();
            if (searchIndexLength.GridUnitType == GridUnitType.Star)
                searchIndexLength = new GridLength(200, GridUnitType.Pixel);
            JsonViewerGrid.RowDefinitions[2].Height = searchIndexLength;
            JsonViewerGrid.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error applying JSON viewer splitter: {ex.Message}");
        }
    }

    private void SaveJsonViewerSplitterSettings()
    {
        try
        {
            if (JsonViewerGrid?.RowDefinitions == null || JsonViewerGrid.RowDefinitions.Count < 5)
                return;
            _layoutSettings.JsonViewerSearchIndexRowHeight = GridLengthDto.FromGridLength(JsonViewerGrid.RowDefinitions[2].Height);
            _layoutSettings.JsonViewerTreeRowHeight = GridLengthDto.FromGridLength(JsonViewerGrid.RowDefinitions[4].Height);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving JSON viewer splitter: {ex.Message}");
        }
    }

    private void SaveCurrentLayoutToSettings(bool isPortrait)
    {
        if (MainGrid == null) return;
        if (isPortrait)
        {
            if (MainGrid.RowDefinitions.Count >= 6)
            {
                _layoutSettings.PortraitTreeRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[0].Height);
                _layoutSettings.PortraitViewerRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[2].Height);
                _layoutSettings.PortraitHistoryRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[4].Height);
            }
        }
        else
        {
            if (MainGrid.ColumnDefinitions.Count >= 1 && MainGrid.RowDefinitions.Count >= 4)
            {
                _layoutSettings.LandscapeLeftColWidth = GridLengthDto.FromGridLength(MainGrid.ColumnDefinitions[0].Width);
                _layoutSettings.LandscapeHistoryRowHeight = GridLengthDto.FromGridLength(MainGrid.RowDefinitions[2].Height);
            }
        }
    }

    private void UpdateLayoutForOrientation(bool isPortrait)
    {
        if (MainGrid == null) return;
        MainGrid.ColumnDefinitions.Clear();
        MainGrid.RowDefinitions.Clear();

        if (isPortrait)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitTreeRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitViewerRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.PortraitHistoryRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(ViewerPanel, 0); Grid.SetRow(ViewerPanel, 2); Grid.SetRowSpan(ViewerPanel, 1);
            Grid.SetColumn(Splitter2, 0); Grid.SetRow(Splitter2, 3); Grid.SetRowSpan(Splitter2, 1);
            Splitter2.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 4);
            Grid.SetColumn(StatusBarBorder, 0); Grid.SetColumnSpan(StatusBarBorder, 1); Grid.SetRow(StatusBarBorder, 5);
        }
        else
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(_layoutSettings.LandscapeLeftColWidth.ToGridLength()));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(_layoutSettings.LandscapeHistoryRowHeight.ToGridLength()));
            MainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 2);
            Grid.SetColumn(Splitter2, 1); Grid.SetRow(Splitter2, 0); Grid.SetRowSpan(Splitter2, 3);
            Splitter2.ResizeDirection = GridResizeDirection.Columns;
            Grid.SetColumn(ViewerPanel, 2); Grid.SetRow(ViewerPanel, 0); Grid.SetRowSpan(ViewerPanel, 3);
            Grid.SetColumn(StatusBarBorder, 0); Grid.SetColumnSpan(StatusBarBorder, 3); Grid.SetRow(StatusBarBorder, 3);
        }
    }
}
