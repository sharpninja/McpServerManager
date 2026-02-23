using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

public partial class ChatWindow : Window
{
    public ChatWindow()
    {
        InitializeComponent();
        InitializeGridRows();
        ApplyLayoutSettings();
        Opened += OnOpened;
        Closing += OnClosing;
        Loaded += OnLoaded;
    }

    private void InitializeGridRows()
    {
        if (ChatMainGrid == null) return;
        var s = LayoutSettingsIo.Load();
        var row1Length = SplitterLayoutPersistence.Resolve(
            s?.ChatTemplatePickerRowHeight,
            new GridLength(1, GridUnitType.Star));
        ChatMainGrid.RowDefinitions.Clear();
        ChatMainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ChatMainGrid.RowDefinitions.Add(new RowDefinition(row1Length));
        ChatMainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(4)));
        ChatMainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        ChatMainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ChatTemplateSplitter != null)
            ChatTemplateSplitter.PointerCaptureLost += (_, _) => SaveLayoutSettings();
        if (PromptTemplatesExpander != null)
        {
            PromptTemplatesExpander.Expanded += (_, _) => ApplyTemplatePickerSplitterSettings(LayoutSettingsIo.Load());
            PromptTemplatesExpander.Collapsed += (_, _) => SetTemplatePickerRowToAuto();
            if (PromptTemplatesExpander.IsExpanded)
                ApplyTemplatePickerSplitterOnly();
            else
                SetTemplatePickerRowToAuto();
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplyTemplatePickerSplitterOnly(), DispatcherPriority.Loaded);
        }
    }

    private void SetTemplatePickerRowToAuto()
    {
        try
        {
            if (ChatMainGrid?.RowDefinitions == null || ChatMainGrid.RowDefinitions.Count < 5) return;
            var row = new RowDefinition(GridLength.Auto) { MinHeight = 40 };
            ChatMainGrid.RowDefinitions[1] = row;
            ChatMainGrid.InvalidateMeasure();
            ChatMainGrid.InvalidateArrange();
        }
        catch { }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyLayoutSettings();
        if (DataContext is ChatWindowViewModel vm)
        {
            _ = vm.LoadModelsAsync();
            vm.LoadPrompts();
            vm.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is ChatWindowViewModel vm)
        {
            vm.CancelSend();
            vm.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }
        SaveLayoutSettings();
    }

    private void ApplyLayoutSettings()
    {
        try
        {
            var s = LayoutSettingsIo.Load();
            if (s == null) return;
            if (s.ChatWindowWidth >= 100 && s.ChatWindowHeight >= 100)
            {
                Width = s.ChatWindowWidth;
                Height = s.ChatWindowHeight;
            }
            if (s.ChatWindowX != 0 || s.ChatWindowY != 0)
                Position = new PixelPoint((int)s.ChatWindowX, (int)s.ChatWindowY);
            ApplyTemplatePickerSplitterSettings(s);
        }
        catch { }
    }

    private void ApplyTemplatePickerSplitterSettings(LayoutSettings? s)
    {
        try
        {
            if (s == null) return;
            if (!SplitterLayoutPersistence.TryApplyRowHeight(
                    ChatMainGrid,
                    1,
                    s.ChatTemplatePickerRowHeight,
                    new GridLength(1, GridUnitType.Star)))
            {
                return;
            }
            ChatMainGrid.InvalidateMeasure();
            ChatMainGrid.InvalidateArrange();
        }
        catch { }
    }

    private void ApplyTemplatePickerSplitterOnly()
    {
        try
        {
            var s = LayoutSettingsIo.Load();
            if (s != null) ApplyTemplatePickerSplitterSettings(s);
        }
        catch { }
    }

    private void SaveLayoutSettings()
    {
        try
        {
            var s = LayoutSettingsIo.Load() ?? new LayoutSettings();
            s.ChatWindowWidth = Width;
            s.ChatWindowHeight = Height;
            s.ChatWindowX = Position.X;
            s.ChatWindowY = Position.Y;
            if (PromptTemplatesExpander?.IsExpanded == true
                && SplitterLayoutPersistence.TryCaptureRowHeight(ChatMainGrid, 1, out var rowHeight)
                && rowHeight != null)
            {
                s.ChatTemplatePickerRowHeight = rowHeight;
            }
            LayoutSettingsIo.Save(s);
        }
        catch { }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ChatMessage msg && msg.Role == "assistant")
                    msg.PropertyChanged += OnAssistantMessagePropertyChanged;
            }
            ScrollToEnd();
        }
    }

    private void OnAssistantMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessage.Text))
            ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MessageScroll != null)
                MessageScroll.Offset = new Vector(MessageScroll.Offset.X, double.MaxValue);
        }, DispatcherPriority.Background);
    }
}
