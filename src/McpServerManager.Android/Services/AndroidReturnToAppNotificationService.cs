using System;
using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

internal static class AndroidReturnToAppNotificationService
{
    private const string ChannelId = "mcp_return_to_app";
    private const int NotificationId = 41001;

    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("AndroidReturnToAppNotificationService");

    public static bool ShowReturnToAppNotification()
    {
        try
        {
            var context = Application.Context;
            if (context == null)
            {
                _logger.LogWarning("Cannot show return-to-app notification: Android application context is null");
                return false;
            }

            var packageName = context.PackageName;
            if (string.IsNullOrWhiteSpace(packageName))
            {
                _logger.LogWarning("Cannot show return-to-app notification: package name is unavailable");
                return false;
            }

            var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
            if (launchIntent == null)
            {
                _logger.LogWarning("Cannot show return-to-app notification: launch intent not found for {PackageName}", packageName);
                return false;
            }

            launchIntent.AddFlags(
                ActivityFlags.NewTask |
                ActivityFlags.SingleTop |
                ActivityFlags.ClearTop |
                ActivityFlags.ReorderToFront);

            var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                pendingIntentFlags |= PendingIntentFlags.Immutable;

            var pendingIntent = PendingIntent.GetActivity(context, 0, launchIntent, pendingIntentFlags);
            if (pendingIntent == null)
            {
                _logger.LogWarning("Cannot show return-to-app notification: pending intent creation returned null");
                return false;
            }

            var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (manager == null)
            {
                _logger.LogWarning("Cannot show return-to-app notification: NotificationManager unavailable");
                return false;
            }

            EnsureChannel(manager);

            var builder = new Notification.Builder(context)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle("Return to Request Tracker")
                .SetContentText("Sign-in is complete. Tap to return to the app.")
                .SetContentIntent(pendingIntent)
                .SetAutoCancel(true)
                .SetOngoing(false)
                .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
                .SetPriority((int)NotificationPriority.High);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                builder.SetChannelId(ChannelId);

            manager.Notify(NotificationId, builder.Build());
            _logger.LogInformation("Posted return-to-app notification fallback after OIDC token acquisition");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post return-to-app notification fallback");
            return false;
        }
    }

    public static void ClearReturnToAppNotification()
    {
        try
        {
            var context = Application.Context;
            var manager = context?.GetSystemService(Context.NotificationService) as NotificationManager;
            manager?.Cancel(NotificationId);
            _logger.LogInformation("Cleared return-to-app notification fallback");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear return-to-app notification fallback");
        }
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        if (manager.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "Return to App",
            NotificationImportance.High)
        {
            Description = "Tap to return to Request Tracker after completing sign-in."
        };

        manager.CreateNotificationChannel(channel);
    }
}
