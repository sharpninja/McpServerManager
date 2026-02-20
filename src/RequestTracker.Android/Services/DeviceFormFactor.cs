using System;
using Android.Util;

namespace RequestTracker.Android.Services;

/// <summary>Detects phone vs tablet form factor based on current display metrics.</summary>
public static class DeviceFormFactor
{
    private const double TabletWidthThresholdDp = 600;

    /// <summary>Raised when the display configuration changes (fold/unfold, rotation).</summary>
    public static event Action? DisplayChanged;

    /// <summary>Returns the current display width in dp.</summary>
    public static double GetCurrentWidthDp()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var metrics = context.Resources?.DisplayMetrics;
            if (metrics == null) return 0;
            return metrics.WidthPixels / metrics.Density;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Returns true if current display width ≥ 600dp.</summary>
    public static bool IsTablet() => GetCurrentWidthDp() >= TabletWidthThresholdDp;

    /// <summary>Called by MainActivity when configuration changes.</summary>
    public static void NotifyDisplayChanged() => DisplayChanged?.Invoke();
}
