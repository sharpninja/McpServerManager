using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace McpServerManager.Core.Converters;

/// <summary>Converts CopilotHeartbeatState string to a colored brush for the status dot.</summary>
public class HeartbeatStateToBrushConverter : IValueConverter
{
    public static readonly HeartbeatStateToBrushConverter Instance = new();

    private static readonly IBrush ConnectingBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)); // gray
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));     // green
    private static readonly IBrush ReceivingBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));  // blue
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));     // orange
    private static readonly IBrush StalledBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));     // red

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = value as string;
        return state switch
        {
            "active" => ActiveBrush,
            "receiving" => ReceivingBrush,
            "warning" => WarningBrush,
            "stalled" => StalledBrush,
            "connecting" => ConnectingBrush,
            _ => ConnectingBrush,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
