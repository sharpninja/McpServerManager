using System;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Desktop.Views;

public partial class McpServerManagerView : UserControl
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("McpServerManagerView");
    private bool? _wasPortrait;
    private bool _isUpdatingLayout;
    private LayoutSettings _layoutSettings = new();

    public McpServerManagerView()
    {
        InitializeComponent();
    }

    /// <summary>Inject saved layout settings and apply initial state.</summary>
    public void ApplySettings(LayoutSettings settings)
    {
        _layoutSettings = settings;
        // A pre-open SizeChanged can set layout using defaults before settings are injected.
        // Force the next host-size pass to rebuild rows/columns from persisted values.
        _wasPortrait = null;
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
            _ = SplitterLayoutPersistence.TryApplyRowHeight(
                JsonViewerGrid,
                2,
                _layoutSettings.JsonViewerSearchIndexRowHeight,
                new GridLength(200, GridUnitType.Pixel),
                coerce: static length => length.GridUnitType == GridUnitType.Star
                    ? new GridLength(200, GridUnitType.Pixel)
                    : length);

            _ = SplitterLayoutPersistence.TryApplyRowHeight(
                JsonViewerGrid,
                4,
                _layoutSettings.JsonViewerTreeRowHeight,
                new GridLength(1, GridUnitType.Star));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error applying JSON viewer splitter: {Message}", ex.Message);
        }
    }

    private void SaveJsonViewerSplitterSettings()
    {
        try
        {
            if (SplitterLayoutPersistence.TryCaptureRowHeight(JsonViewerGrid, 2, out var searchIndex) && searchIndex != null)
                _layoutSettings.JsonViewerSearchIndexRowHeight = searchIndex;
            if (SplitterLayoutPersistence.TryCaptureRowHeight(JsonViewerGrid, 4, out var treeRow) && treeRow != null)
                _layoutSettings.JsonViewerTreeRowHeight = treeRow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error saving JSON viewer splitter: {Message}", ex.Message);
        }
    }

    private void SaveCurrentLayoutToSettings(bool isPortrait)
    {
        if (MainGrid == null) return;
        if (isPortrait)
        {
            if (SplitterLayoutPersistence.TryCaptureRowHeight(MainGrid, 0, out var treeHeight) && treeHeight != null)
                _layoutSettings.PortraitTreeRowHeight = treeHeight;
            if (SplitterLayoutPersistence.TryCaptureRowHeight(MainGrid, 2, out var viewerHeight) && viewerHeight != null)
                _layoutSettings.PortraitViewerRowHeight = viewerHeight;
            if (SplitterLayoutPersistence.TryCaptureRowHeight(MainGrid, 4, out var historyHeight) && historyHeight != null)
                _layoutSettings.PortraitHistoryRowHeight = historyHeight;
        }
        else
        {
            if (SplitterLayoutPersistence.TryCaptureColumnWidth(MainGrid, 0, out var leftWidth) && leftWidth != null)
                _layoutSettings.LandscapeLeftColWidth = leftWidth;
            if (SplitterLayoutPersistence.TryCaptureRowHeight(MainGrid, 2, out var historyHeight) && historyHeight != null)
                _layoutSettings.LandscapeHistoryRowHeight = historyHeight;
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
            MainGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.PortraitTreeRowHeight,
                    new GridLength(1, GridUnitType.Star))));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.PortraitViewerRowHeight,
                    new GridLength(1, GridUnitType.Star))));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.PortraitHistoryRowHeight,
                    new GridLength(150, GridUnitType.Pixel))));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(ViewerPanel, 0); Grid.SetRow(ViewerPanel, 2); Grid.SetRowSpan(ViewerPanel, 1);
            Grid.SetColumn(Splitter2, 0); Grid.SetRow(Splitter2, 3); Grid.SetRowSpan(Splitter2, 1);
            Splitter2.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 4);
        }
        else
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.LandscapeLeftColWidth,
                    new GridLength(300, GridUnitType.Pixel))));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            MainGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            MainGrid.RowDefinitions.Add(new RowDefinition(
                SplitterLayoutPersistence.Resolve(
                    _layoutSettings.LandscapeHistoryRowHeight,
                    new GridLength(150, GridUnitType.Pixel))));

            Grid.SetColumn(TreePanel, 0); Grid.SetRow(TreePanel, 0);
            Grid.SetColumn(Splitter1, 0); Grid.SetRow(Splitter1, 1);
            Splitter1.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(HistoryPanel, 0); Grid.SetRow(HistoryPanel, 2);
            Grid.SetColumn(Splitter2, 1); Grid.SetRow(Splitter2, 0); Grid.SetRowSpan(Splitter2, 3);
            Splitter2.ResizeDirection = GridResizeDirection.Columns;
            Grid.SetColumn(ViewerPanel, 2); Grid.SetRow(ViewerPanel, 0); Grid.SetRowSpan(ViewerPanel, 3);
        }
    }
}
