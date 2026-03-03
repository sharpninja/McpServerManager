using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using McpServerManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Android.Services;

/// <summary>
/// Foreground service that keeps the app process alive while a voice conversation
/// session is active, allowing STT/TTS to continue in the background.
/// </summary>
[Service(Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone,
    Name = "ninja.thesharp.mcpservermanager.voice.VoiceSessionForegroundService")]
public sealed class VoiceSessionForegroundService : Service
{
    public const string ActionStart = "ninja.thesharp.mcpservermanager.voice.START_SESSION_FOREGROUND";
    public const string ActionStop = "ninja.thesharp.mcpservermanager.voice.STOP_SESSION_FOREGROUND";
    public const string ActionUpdateStatus = "ninja.thesharp.mcpservermanager.voice.UPDATE_SESSION_STATUS";
    public const string ExtraStatusText = "status_text";

    private const string ChannelId = "voice_session_active";
    private const int NotificationId = 42010;

    public static Intent CreateStartIntent(Context context, string? statusText = null)
    {
        var intent = new Intent(context, typeof(VoiceSessionForegroundService));
        intent.SetAction(ActionStart);
        if (!string.IsNullOrWhiteSpace(statusText))
            intent.PutExtra(ExtraStatusText, statusText);
        return intent;
    }

    public static Intent CreateStopIntent(Context context)
    {
        var intent = new Intent(context, typeof(VoiceSessionForegroundService));
        intent.SetAction(ActionStop);
        return intent;
    }

    public static Intent CreateUpdateIntent(Context context, string statusText)
    {
        var intent = new Intent(context, typeof(VoiceSessionForegroundService));
        intent.SetAction(ActionUpdateStatus);
        intent.PutExtra(ExtraStatusText, statusText);
        return intent;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartCommandResult Core()
        {
            var action = intent?.Action;

            if (string.Equals(action, ActionStop, StringComparison.Ordinal))
            {
                StopForegroundCompat();
                StopSelfResult(startId);
                return StartCommandResult.NotSticky;
            }

            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            if (notificationManager != null)
                EnsureChannel(notificationManager);

            var statusText = intent?.GetStringExtra(ExtraStatusText);
            var notification = BuildNotification(statusText);

            if (string.Equals(action, ActionUpdateStatus, StringComparison.Ordinal))
            {
                notificationManager?.Notify(NotificationId, notification);
                return StartCommandResult.Sticky;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeMicrophone);
            else
                StartForeground(NotificationId, notification);

            return StartCommandResult.Sticky;
        }

        return AndroidCrashDiagnostics.ExecuteFatal(
            "VoiceSessionForegroundService.OnStartCommand",
            Core,
            "Voice session foreground service crashed while processing a start/update/stop command.");
    }

    public override void OnDestroy()
    {
        void Core()
        {
            StopForegroundCompat();
            base.OnDestroy();
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "VoiceSessionForegroundService.OnDestroy",
            Core,
            "Voice session foreground service crashed while shutting down.");
    }

    private Notification BuildNotification(string? statusText)
    {
        var contentText = string.IsNullOrWhiteSpace(statusText)
            ? "Voice session is active."
            : statusText;

        var pendingIntent = CreateLaunchPendingIntent();
        var builder = new Notification.Builder(this)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("Voice Chat Active")
            .SetContentText(contentText)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis());

        if (pendingIntent != null)
            builder.SetContentIntent(pendingIntent);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            builder.SetChannelId(ChannelId);
        else
            builder.SetPriority((int)NotificationPriority.Low);

        return builder.Build();
    }

    private PendingIntent? CreateLaunchPendingIntent()
    {
        var packageName = PackageName;
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(packageName);
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

        return PendingIntent.GetActivity(this, 42011, launchIntent, flags);
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        if (manager.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(ChannelId, "Voice Session", NotificationImportance.Low)
        {
            Description = "Foreground service active while a voice conversation session is running."
        };
        manager.CreateNotificationChannel(channel);
    }

    private void StopForegroundCompat()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            StopForeground(StopForegroundFlags.Remove);
        else
#pragma warning disable CS0618
            StopForeground(true);
#pragma warning restore CS0618
    }
}
