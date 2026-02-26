using System;
using Android.Content;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

internal static class AndroidBrowserService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("AndroidBrowserService");

    public static bool TryOpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var context = global::Android.App.Application.Context;
            if (context == null)
                return false;

            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url.Trim()));
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
            _logger.LogInformation("Opened external browser for OIDC sign-in URL");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open external browser for OIDC sign-in URL");
            return false;
        }
    }

    public static bool TryBringAppToForeground()
    {
        var notificationPosted = AndroidReturnToAppNotificationService.ShowReturnToAppNotification();

        try
        {
            var context = global::Android.App.Application.Context;
            if (context == null)
            {
                _logger.LogWarning("Cannot foreground app after OIDC token acquisition: Android application context is null");
                return notificationPosted;
            }

            var packageName = context.PackageName;
            if (string.IsNullOrWhiteSpace(packageName))
            {
                _logger.LogWarning("Cannot foreground app after OIDC token acquisition: package name is unavailable");
                return notificationPosted;
            }

            var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
            if (launchIntent == null)
            {
                _logger.LogWarning("Cannot foreground app after OIDC token acquisition: launch intent not found for {PackageName}", packageName);
                return notificationPosted;
            }

            launchIntent.AddFlags(
                ActivityFlags.NewTask |
                ActivityFlags.SingleTop |
                ActivityFlags.ClearTop |
                ActivityFlags.ReorderToFront);

            context.StartActivity(launchIntent);
            _logger.LogInformation("Requested Android app foreground after OIDC token acquisition");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed requesting Android app foreground after OIDC token acquisition");
            return notificationPosted;
        }
    }
}
