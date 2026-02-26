using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;

namespace McpServerManager.Android.Services;

/// <summary>
/// Tracks the current Android activity and permission callbacks for app services.
/// </summary>
public static class AndroidActivityHost
{
    private static readonly object Sync = new();
    private static WeakReference<Activity>? _currentActivity;
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> PendingPermissionRequests = new();
    private static int _nextRequestCode = 7000;

    /// <summary>
    /// Registers the current foreground activity instance.
    /// </summary>
    public static void Register(Activity activity)
    {
        if (activity == null) return;
        lock (Sync)
        {
            _currentActivity = new WeakReference<Activity>(activity);
        }
    }

    /// <summary>
    /// Gets the current activity if available.
    /// </summary>
    public static Activity? TryGetCurrentActivity()
    {
        lock (Sync)
        {
            if (_currentActivity == null) return null;
            return _currentActivity.TryGetTarget(out var activity) ? activity : null;
        }
    }

    /// <summary>
    /// Requests microphone permission and completes when the system responds.
    /// </summary>
    public static Task<bool> RequestRecordAudioPermissionAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return RequestPermissionAsync(activity, global::Android.Manifest.Permission.RecordAudio, cancellationToken);
    }

    /// <summary>
    /// Requests notification permission on Android 13+ and completes when the system responds.
    /// </summary>
    public static Task<bool> RequestPostNotificationsPermissionAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if ((int)global::Android.OS.Build.VERSION.SdkInt < (int)global::Android.OS.BuildVersionCodes.Tiramisu)
            return Task.FromResult(true);

        return RequestPermissionAsync(activity, global::Android.Manifest.Permission.PostNotifications, cancellationToken);
    }

    private static Task<bool> RequestPermissionAsync(Activity activity, string permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission name is required.", nameof(permission));

        if ((int)global::Android.OS.Build.VERSION.SdkInt < (int)global::Android.OS.BuildVersionCodes.M)
            return Task.FromResult(true);

        var status = activity.CheckSelfPermission(permission);
        if (status == Permission.Granted)
            return Task.FromResult(true);

        var requestCode = Interlocked.Increment(ref _nextRequestCode);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingPermissionRequests[requestCode] = tcs;

        using var _ = cancellationToken.Register(() =>
        {
            if (PendingPermissionRequests.TryRemove(requestCode, out var pending))
                pending.TrySetCanceled(cancellationToken);
        });

        activity.RequestPermissions([permission], requestCode);
        return tcs.Task;
    }

    /// <summary>
    /// Handles permission callback forwarding from MainActivity.
    /// </summary>
    public static void OnRequestPermissionsResult(int requestCode, string[]? permissions, Permission[]? grantResults)
    {
        if (!PendingPermissionRequests.TryRemove(requestCode, out var tcs))
            return;

        var granted = false;
        if (grantResults is { Length: > 0 })
            granted = grantResults[0] == Permission.Granted;

        tcs.TrySetResult(granted);
    }
}
