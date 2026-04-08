using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.Logging;
using McpServerManager.UI.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

/// <summary>
/// Android implementation for posting actionable agent event notifications.
/// </summary>
public sealed class AndroidSystemNotificationService : ISystemNotificationService
{
    private const string ChannelId = "mcp_agent_events";
    private const int LaunchRequestCode = 43001;
    private static int _nextNotificationId = 43000;

    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("AndroidSystemNotificationService");

    public async Task NotifyAgentEventAsync(
        McpIncomingChangeEvent changeEvent,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeEvent);

        var notificationMessage = string.IsNullOrWhiteSpace(message)
            ? "Agent event received."
            : message.Trim();

        try
        {
            var context = Application.Context;
            if (context == null)
            {
                _logger.LogWarning("Skipping agent event notification: Android application context is null.");
                return;
            }

            if (!await EnsureNotificationPermissionAsync(context, cancellationToken))
                return;

            var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (manager == null)
            {
                _logger.LogWarning("Skipping agent event notification: NotificationManager unavailable.");
                return;
            }

            EnsureChannel(manager);

            var builder = new Notification.Builder(context)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle("Request Tracker")
                .SetContentText(notificationMessage)
                .SetStyle(new Notification.BigTextStyle().BigText(notificationMessage))
                .SetAutoCancel(true)
                .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
                .SetPriority((int)NotificationPriority.Default);

            var launchPendingIntent = CreateLaunchPendingIntent(context);
            if (launchPendingIntent != null)
                builder.SetContentIntent(launchPendingIntent);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                builder.SetChannelId(ChannelId);

            manager.Notify(Interlocked.Increment(ref _nextNotificationId), builder.Build());
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post agent event notification.");
        }
    }

    private static async Task<bool> EnsureNotificationPermissionAsync(Context context, CancellationToken cancellationToken)
    {
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.Tiramisu)
            return true;

        if (context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted)
            return true;

        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null)
        {
            _logger.LogDebug("Skipping agent event notification: no activity available to request POST_NOTIFICATIONS.");
            return false;
        }

        try
        {
            var granted = await AndroidActivityHost
                .RequestPostNotificationsPermissionAsync(activity, cancellationToken)
                ;

            if (!granted)
                _logger.LogDebug("Skipping agent event notification: POST_NOTIFICATIONS permission denied.");

            return granted;
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
    }

    private static PendingIntent? CreateLaunchPendingIntent(Context context)
    {
        var packageName = context.PackageName;
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        if (launchIntent == null)
            return null;

        launchIntent.AddFlags(
            ActivityFlags.NewTask |
            ActivityFlags.SingleTop |
            ActivityFlags.ClearTop |
            ActivityFlags.ReorderToFront);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;

        return PendingIntent.GetActivity(context, LaunchRequestCode, launchIntent, flags);
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        if (manager.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "Agent Events",
            NotificationImportance.Default)
        {
            Description = "Notifications for actionable agent events."
        };

        manager.CreateNotificationChannel(channel);
    }
}

