using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CoreTodoItem = McpServerManager.Core.Models.McpTodoFlatItem;
using UiCoreTodoItem = McpServerManager.UI.Core.Models.McpTodoFlatItem;

namespace McpServerManager.Core.Converters;

/// <summary>Converts todo done state to a light-green row background.</summary>
public class DoneToBackgroundBrushConverter : IValueConverter
{
    public static readonly DoneToBackgroundBrushConverter Instance = new();
    private static readonly IBrush CompletedBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xF4, 0xEA));
    private static readonly IBrush ImplementationPlanBrush = Brushes.Yellow;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CoreTodoItem item)
        {
            if (IsImplementationPlanId(item.Id))
                return ImplementationPlanBrush;

            return item.Done ? CompletedBrush : Brushes.Transparent;
        }

        if (value is UiCoreTodoItem uiItem)
        {
            if (IsImplementationPlanId(uiItem.Id))
                return ImplementationPlanBrush;

            return uiItem.Done ? CompletedBrush : Brushes.Transparent;
        }

        if (value is bool done)
            return done ? CompletedBrush : Brushes.Transparent;

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;

    private static bool IsImplementationPlanId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return id.IndexOf("PLAN", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("IMPL", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
