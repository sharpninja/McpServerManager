using Android.Content.Res;
using Android.Util;

namespace RequestTracker.Android.Services;

/// <summary>Detects phone vs tablet form factor based on screen size.</summary>
public static class DeviceFormFactor
{
    /// <summary>Returns true if the device screen is ≥ 600dp wide (tablet threshold).</summary>
    public static bool IsTablet()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var metrics = context.Resources?.DisplayMetrics;
            if (metrics == null) return false;

            float widthDp = metrics.WidthPixels / metrics.Density;
            float heightDp = metrics.HeightPixels / metrics.Density;
            float smallestWidth = System.Math.Min(widthDp, heightDp);
            return smallestWidth >= 600;
        }
        catch
        {
            return false;
        }
    }
}
