using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using McpServerManager.Android.Services;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class PhoneTodoView : UserControl
{
    private enum PhoneTodoScreen
    {
        List,
        DetailFormatted,
        DetailMarkdownEdit
    }

    private PhoneTodoScreen _screen = PhoneTodoScreen.List;
    private bool _hasAutoLoaded;
    private TodoListViewModel? _currentVm;

    public PhoneTodoView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        AttachedToVisualTree += OnAttachedToVisualTree;
        UpdateScreenVisibility();
        RefreshFormattedDetailFromViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AndroidBackNavigationService.BackRequested -= OnAndroidBackRequested;
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AndroidBackNavigationService.BackRequested -= OnAndroidBackRequested;
        AndroidBackNavigationService.BackRequested += OnAndroidBackRequested;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // No auto-load here — workspace-change event triggers the initial load
        // after the correct workspace path is set on the shared MCP client.
        _hasAutoLoaded = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;

        _currentVm = DataContext as TodoListViewModel;
        if (_currentVm == null)
        {
            RefreshFormattedDetailFromViewModel();
            return;
        }

        _currentVm.GetEditorText = () => Editor.Text ?? "";
        _currentVm.PropertyChanged += OnViewModelPropertyChanged;

        SyncEditorFromViewModel(_currentVm);
        RefreshFormattedDetailFromViewModel();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TodoListViewModel vm) return;

        if (e.PropertyName == nameof(TodoListViewModel.EditorText))
        {
            if (!string.Equals(Editor.Text ?? "", vm.EditorText ?? "", StringComparison.Ordinal))
                Editor.Text = vm.EditorText;
        }
        else if (e.PropertyName == nameof(TodoListViewModel.EditorFontSize))
        {
            Editor.FontSize = Math.Max(vm.EditorFontSize, 16);
        }
        else if (e.PropertyName == nameof(TodoListViewModel.CurrentTodoDetail))
        {
            RefreshFormattedDetailFromViewModel();
        }
    }

    private void SyncEditorFromViewModel(TodoListViewModel vm)
    {
        Editor.Text = vm.EditorText;
        Editor.FontSize = Math.Max(vm.EditorFontSize, 16);
    }

    private void ShowScreen(PhoneTodoScreen screen)
    {
        _screen = screen;
        UpdateScreenVisibility();
    }

    private void UpdateScreenVisibility()
    {
        if (ListScreen == null || DetailScreen == null || EditScreen == null)
            return;

        ListScreen.IsVisible = _screen == PhoneTodoScreen.List;
        DetailScreen.IsVisible = _screen == PhoneTodoScreen.DetailFormatted;
        EditScreen.IsVisible = _screen == PhoneTodoScreen.DetailMarkdownEdit;
    }

    private async void OnListRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.RefreshCommand.ExecuteAsync(null);
    }

    private async void OnTodoRowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        if (sender is not Button button || button.Tag is not TodoListEntry entry) return;

        vm.SelectedEntry = entry;
        if (!vm.OpenSelectedTodoCommand.CanExecute(null))
            return;

        await vm.OpenSelectedTodoCommand.ExecuteAsync(null);
        RefreshFormattedDetailFromViewModel();

        if (vm.CurrentTodoDetail != null)
            ShowScreen(PhoneTodoScreen.DetailFormatted);
    }

    private void OnDetailBackClick(object? sender, RoutedEventArgs e)
    {
        ShowScreen(PhoneTodoScreen.List);
    }

    private async void OnDetailCopyIdClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.CopySelectedIdCommand.ExecuteAsync(null);
    }

    private async void OnDetailToggleDoneClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.ToggleDoneCommand.ExecuteAsync(null);
        RefreshFormattedDetailFromViewModel();

        if (vm.CurrentTodoDetail == null)
            ShowScreen(PhoneTodoScreen.List);
    }

    private async void OnDetailRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.RefreshEditorCommand.ExecuteAsync(null);
        RefreshFormattedDetailFromViewModel();

        if (vm.CurrentTodoDetail == null)
            ShowScreen(PhoneTodoScreen.List);
    }

    private void OnDetailEditMarkdownClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        SyncEditorFromViewModel(vm);
        ShowScreen(PhoneTodoScreen.DetailMarkdownEdit);
    }

    private async void OnDetailPlanClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        ShowScreen(PhoneTodoScreen.DetailMarkdownEdit);
        await vm.CopilotPlanCommand.ExecuteAsync(null);
    }

    private async void OnDetailStatusClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        ShowScreen(PhoneTodoScreen.DetailMarkdownEdit);
        await vm.CopilotStatusCommand.ExecuteAsync(null);
    }

    private async void OnDetailImplementClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        ShowScreen(PhoneTodoScreen.DetailMarkdownEdit);
        await vm.CopilotImplementCommand.ExecuteAsync(null);
    }

    private void OnEditBackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TodoListViewModel vm && vm.CurrentTodoDetail != null)
        {
            ShowScreen(PhoneTodoScreen.DetailFormatted);
            return;
        }

        ShowScreen(PhoneTodoScreen.List);
    }

    private bool OnAndroidBackRequested()
    {
        if (!IsVisible)
            return false;

        switch (_screen)
        {
            case PhoneTodoScreen.DetailMarkdownEdit:
                OnEditBackClick(this, new RoutedEventArgs());
                return true;
            case PhoneTodoScreen.DetailFormatted:
                OnDetailBackClick(this, new RoutedEventArgs());
                return true;
            default:
                return false;
        }
    }

    private async void OnEditSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.SaveEditorCommand.ExecuteAsync(null);
        RefreshFormattedDetailFromViewModel();

        if (vm.CurrentTodoDetail != null && LooksLikeSuccessfulSave(vm.StatusText))
            ShowScreen(PhoneTodoScreen.DetailFormatted);
    }

    private async void OnEditRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TodoListViewModel vm) return;
        await vm.RefreshEditorCommand.ExecuteAsync(null);
        RefreshFormattedDetailFromViewModel();

        if (vm.CurrentTodoDetail == null)
            ShowScreen(PhoneTodoScreen.List);
    }

    private static bool LooksLikeSuccessfulSave(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText)) return false;
        return statusText.StartsWith("Saved ", StringComparison.OrdinalIgnoreCase)
               || statusText.StartsWith("Created ", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshFormattedDetailFromViewModel()
    {
        if (DetailTitleText == null || DetailSubtitleText == null ||
            DetailMetaChipsPanel == null || DetailFieldCardsPanel == null ||
            DetailSectionCardsPanel == null || DetailEmptyText == null ||
            DetailDoneToggle == null)
            return;

        if (DataContext is not TodoListViewModel vm || vm.CurrentTodoDetail == null)
        {
            DetailTitleText.Text = "No TODO selected";
            DetailSubtitleText.Text = "Open a TODO from the list.";
            DetailDoneToggle.IsChecked = false;
            DetailDoneToggle.IsEnabled = false;
            DetailMetaChipsPanel.Children.Clear();
            DetailFieldCardsPanel.Children.Clear();
            DetailSectionCardsPanel.Children.Clear();
            DetailEmptyText.Text = "";
            DetailEmptyText.IsVisible = false;
            return;
        }

        var item = vm.CurrentTodoDetail;
        DetailDoneToggle.IsEnabled = true;
        DetailDoneToggle.IsChecked = item.Done;
        DetailTitleText.Text = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title;
        DetailSubtitleText.Text = BuildSubtitle(item);
        RenderFormattedDetail(item);
    }

    private static string BuildSubtitle(McpTodoFlatItem item)
    {
        var parts = new[]
        {
            item.Id,
            item.Section,
            item.Priority,
            item.Done ? "done" : "open"
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(" | ", parts);
    }

    private void RenderFormattedDetail(McpTodoFlatItem item)
    {
        DetailMetaChipsPanel.Children.Clear();
        DetailFieldCardsPanel.Children.Clear();
        DetailSectionCardsPanel.Children.Clear();

        AddMetaChip(item.Id);
        if (!string.IsNullOrWhiteSpace(item.Priority))
            AddMetaChip($"Priority: {item.Priority}");
        if (!string.IsNullOrWhiteSpace(item.Section))
            AddMetaChip($"Section: {item.Section}");
        AddMetaChip(item.Done ? "Done" : "Open");

        var scalarRows = new List<(string Label, string Value)>
        {
            ("Estimate", item.Estimate ?? ""),
            ("Note", item.Note ?? ""),
            ("Completed", item.CompletedDate ?? ""),
            ("Done Summary", item.DoneSummary ?? ""),
            ("Remaining", item.Remaining ?? "")
        }
        .Where(r => !string.IsNullOrWhiteSpace(r.Value))
        .ToList();

        if (scalarRows.Count > 0)
            DetailFieldCardsPanel.Children.Add(CreateFieldCard("Details", scalarRows));

        AddStringSectionCard("Description", item.Description, bullets: false);
        AddStringSectionCard("Technical Details", item.TechnicalDetails, bullets: true);
        AddTaskSectionCard("Implementation Tasks", item.ImplementationTasks);
        AddStringSectionCard("Depends On", item.DependsOn, bullets: true);
        AddStringSectionCard("Functional Requirements", item.FunctionalRequirements, bullets: true);
        AddStringSectionCard("Technical Requirements", item.TechnicalRequirements, bullets: true);

        var hasCards = DetailFieldCardsPanel.Children.Count > 0 || DetailSectionCardsPanel.Children.Count > 0;
        DetailEmptyText.Text = hasCards ? "" : "No additional detail fields are populated for this TODO.";
        DetailEmptyText.IsVisible = !hasCards;
    }

    private void AddMetaChip(string text)
    {
        DetailMetaChipsPanel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = GetCardBorderBrush(),
            Background = GetChipBackgroundBrush(),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = GetPrimaryTextBrush()
            }
        });
    }

    private Control CreateFieldCard(string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        var content = new StackPanel { Spacing = 6 };
        foreach (var row in rows)
        {
            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 10
            };
            rowGrid.Children.Add(new TextBlock
            {
                Text = row.Label,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = GetPrimaryTextBrush()
            });

            var valueText = new TextBlock
            {
                Text = row.Value,
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetSubtleTextBrush()
            };
            Grid.SetColumn(valueText, 1);
            rowGrid.Children.Add(valueText);
            content.Children.Add(rowGrid);
        }

        return CreateCard(title, content);
    }

    private void AddStringSectionCard(string title, IReadOnlyCollection<string>? items, bool bullets)
    {
        if (items == null || items.Count == 0)
            return;

        var filtered = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();
        if (filtered.Count == 0) return;

        var content = new StackPanel { Spacing = 6 };
        foreach (var line in filtered)
        {
            var row = new Grid
            {
                ColumnDefinitions = bullets ? new ColumnDefinitions("Auto,*") : new ColumnDefinitions("*"),
                ColumnSpacing = 8
            };

            if (bullets)
            {
                row.Children.Add(new TextBlock
                {
                    Text = "•",
                    FontWeight = FontWeight.Bold,
                    Foreground = GetPrimaryTextBrush()
                });
            }

            var lineText = new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetPrimaryTextBrush()
            };
            Grid.SetColumn(lineText, bullets ? 1 : 0);
            row.Children.Add(lineText);
            content.Children.Add(row);
        }

        DetailSectionCardsPanel.Children.Add(CreateCard(title, content));
    }

    private void AddTaskSectionCard(string title, IReadOnlyCollection<McpTodoFlatTask>? tasks)
    {
        if (tasks == null || tasks.Count == 0)
            return;

        var content = new StackPanel { Spacing = 6 };
        foreach (var task in tasks)
        {
            var taskText = (task.Task ?? "").Trim();
            if (taskText.Length == 0) continue;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8
            };
            row.Children.Add(new TextBlock
            {
                Text = task.Done ? "[x]" : "[ ]",
                FontWeight = FontWeight.SemiBold,
                Foreground = GetPrimaryTextBrush()
            });
            var taskTextBlock = new TextBlock
            {
                Text = taskText,
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetPrimaryTextBrush()
            };
            Grid.SetColumn(taskTextBlock, 1);
            row.Children.Add(taskTextBlock);
            content.Children.Add(row);
        }

        if (content.Children.Count > 0)
            DetailSectionCardsPanel.Children.Add(CreateCard(title, content));
    }

    private Control CreateCard(string title, Control content)
    {
        var root = new StackPanel { Spacing = 6 };
        root.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            Foreground = GetPrimaryTextBrush()
        });
        root.Children.Add(content);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = GetCardBorderBrush(),
            Background = GetCardBackgroundBrush(),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Child = root
        };
    }

    private IBrush GetPrimaryTextBrush()
        => TryGetBrush("ThemeForegroundBrush")
           ?? TryGetBrush("SystemControlForegroundBaseHighBrush")
           ?? (IsDarkTheme() ? Brushes.White : Brushes.Black);

    private IBrush GetSubtleTextBrush()
        => TryGetBrush("SubtleForeground")
           ?? TryGetBrush("SystemControlForegroundBaseMediumBrush")
           ?? (IsDarkTheme()
               ? new SolidColorBrush(Color.FromArgb(0xDD, 0xE0, 0xE0, 0xE0))
               : new SolidColorBrush(Color.FromArgb(0xDD, 0x40, 0x40, 0x40)));

    private IBrush GetCardBorderBrush()
        => TryGetBrush("ItemBorderBrush")
           ?? TryGetBrush("SeparatorBrush")
           ?? (IsDarkTheme()
               ? new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF))
               : new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00)));

    private IBrush GetCardBackgroundBrush()
        => TryGetBrush("ThemeBackgroundBrush")
           ?? TryGetBrush("SystemAltHighColor")
           ?? Brushes.Transparent;

    private IBrush GetChipBackgroundBrush()
        => TryGetBrush("SystemAltMediumHighColor")
           ?? TryGetBrush("SystemAltHighColor")
           ?? Brushes.Transparent;

    private bool IsDarkTheme()
        => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    private IBrush? TryGetBrush(string resourceKey)
    {
        if (Resources.TryGetResource(resourceKey, null, out var local) && local is IBrush localBrush)
            return localBrush;
        if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var app) == true && app is IBrush appBrush)
            return appBrush;
        return null;
    }
}
