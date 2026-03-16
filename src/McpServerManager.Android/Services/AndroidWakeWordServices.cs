using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

public sealed class AndroidWakeWordDetectedEventArgs(string phrase, DateTimeOffset detectedAtUtc) : EventArgs
{
    public string Phrase { get; } = phrase;
    public DateTimeOffset DetectedAtUtc { get; } = detectedAtUtc;
}

public sealed class AndroidWakeWordSettings
{
    public string SelectedWakePhrase { get; init; } = AndroidWakeWordCatalog.DefaultWakePhrase;
    public string Sensitivity { get; init; } = "medium";
}

public static class AndroidWakeWordCatalog
{
    private static readonly string[] Phrases =
    [
        "Hey Tracker",
        "Okay Tracker",
        "Hello Tracker"
    ];

    public static IReadOnlyList<string> SupportedWakePhrases => Phrases;
    public static string DefaultWakePhrase => Phrases[0];
}

public interface IAndroidWakeWordService : IDisposable
{
    bool IsMonitoring { get; }
    IReadOnlyList<string> AvailableWakePhrases { get; }
    string SelectedWakePhrase { get; }
    string SelectedWakeSensitivity { get; }
    event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;
    Task ApplySettingsAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default);
    Task<bool> SetSelectedWakePhraseAsync(string phrase, CancellationToken cancellationToken = default);
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);
    void SimulateWakeWordDetected(string? phrase = null);
}

public interface IAndroidVoiceForegroundServiceController
{
    bool IsRunning { get; }
    Task<bool> StartAsync(string? statusText = null, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pluggable wake-word engine contract. Real engine implementations should plug in here.
/// </summary>
public interface IAndroidWakeWordEngine : IDisposable
{
    bool IsRunning { get; }
    event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;
    Task ConfigureAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub engine placeholder until a production wake-word SDK is integrated.
/// </summary>
public sealed class AndroidWakeWordEngineStub : IAndroidWakeWordEngine
{
    private AndroidWakeWordSettings _settings = new();

    public bool IsRunning { get; private set; }

    public event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;

    public Task ConfigureAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settings = settings ?? new AndroidWakeWordSettings();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsRunning = false;
    }

    public void Simulate(string? phrase = null)
    {
        if (!IsRunning)
            return;

        var effectivePhrase = string.IsNullOrWhiteSpace(phrase)
            ? _settings.SelectedWakePhrase
            : phrase.Trim();

        WakeWordDetected?.Invoke(this, new AndroidWakeWordDetectedEventArgs(effectivePhrase, DateTimeOffset.UtcNow));
    }
}

public interface IAndroidWakeWordSettingsStore
{
    AndroidWakeWordSettings Load();
    void Save(AndroidWakeWordSettings settings);
}

public sealed class AndroidWakeWordPreferencesStore : IAndroidWakeWordSettingsStore
{
    private const string PreferencesName = "McpServerManager.Voice";
    private const string WakePhraseKey = "WakePhrase";
    private const string WakeSensitivityKey = "WakeSensitivity";

    public AndroidWakeWordSettings Load()
    {
        try
        {
            var prefs = GetPreferences();
            var storedPhrase = prefs?.GetString(WakePhraseKey, null)?.Trim();
            var storedSensitivity = prefs?.GetString(WakeSensitivityKey, null)?.Trim();

            return new AndroidWakeWordSettings
            {
                SelectedWakePhrase = NormalizeWakePhrase(storedPhrase),
                Sensitivity = string.IsNullOrWhiteSpace(storedSensitivity) ? "medium" : storedSensitivity
            };
        }
        catch
        {
            return new AndroidWakeWordSettings();
        }
    }

    public void Save(AndroidWakeWordSettings settings)
    {
        if (settings == null)
            return;

        var prefs = GetPreferences();
        var editor = prefs?.Edit();
        if (editor == null)
            return;

        using (editor)
        {
            editor.PutString(WakePhraseKey, NormalizeWakePhrase(settings.SelectedWakePhrase));
            editor.PutString(WakeSensitivityKey, string.IsNullOrWhiteSpace(settings.Sensitivity) ? "medium" : settings.Sensitivity.Trim());
            editor.Apply();
        }
    }

    private static string NormalizeWakePhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return AndroidWakeWordCatalog.DefaultWakePhrase;

        var match = AndroidWakeWordCatalog.SupportedWakePhrases
            .FirstOrDefault(p => string.Equals(p, phrase.Trim(), StringComparison.OrdinalIgnoreCase));

        return match ?? AndroidWakeWordCatalog.DefaultWakePhrase;
    }

    private static ISharedPreferences? GetPreferences()
    {
        var context = Application.Context;
        return context?.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }
}

/// <summary>
/// Wake-word scaffold service for Android. v1 only manages lifecycle and foreground-service plumbing.
/// </summary>
public sealed class AndroidWakeWordService : IAndroidWakeWordService
{
    private readonly IAndroidVoiceForegroundServiceController _foregroundServiceController;
    private readonly IAndroidWakeWordEngine _wakeWordEngine;
    private readonly IAndroidWakeWordSettingsStore _settingsStore;
    private AndroidWakeWordSettings _settings;
    private bool _disposed;

    public bool IsMonitoring { get; private set; }
    public IReadOnlyList<string> AvailableWakePhrases => AndroidWakeWordCatalog.SupportedWakePhrases;
    public string SelectedWakePhrase => _settings.SelectedWakePhrase;
    public string SelectedWakeSensitivity => _settings.Sensitivity;

    public event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;

    public AndroidWakeWordService()
        : this(
            new AndroidVoiceForegroundServiceController(),
            new AndroidVoskWakeWordEngine(),
            new AndroidWakeWordPreferencesStore())
    {
    }

    public AndroidWakeWordService(
        IAndroidVoiceForegroundServiceController foregroundServiceController,
        IAndroidWakeWordEngine wakeWordEngine,
        IAndroidWakeWordSettingsStore settingsStore)
    {
        _foregroundServiceController = foregroundServiceController ?? throw new ArgumentNullException(nameof(foregroundServiceController));
        _wakeWordEngine = wakeWordEngine ?? throw new ArgumentNullException(nameof(wakeWordEngine));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = NormalizeSettings(_settingsStore.Load());
        _wakeWordEngine.WakeWordDetected += OnWakeWordDetected;
    }

    public async Task<bool> SetSelectedWakePhraseAsync(string phrase, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = NormalizeWakePhrase(phrase);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        await ApplySettingsAsync(new AndroidWakeWordSettings
        {
            SelectedWakePhrase = normalized,
            Sensitivity = _settings.Sensitivity
        }, cancellationToken);

        return true;
    }

    public async Task ApplySettingsAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = NormalizeSettings(settings);
        if (string.Equals(_settings.SelectedWakePhrase, normalized.SelectedWakePhrase, StringComparison.Ordinal) &&
            string.Equals(_settings.Sensitivity, normalized.Sensitivity, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings = normalized;
        _settingsStore.Save(_settings);

        if (IsMonitoring)
            await _wakeWordEngine.ConfigureAsync(_settings, cancellationToken);
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsMonitoring)
            return;

        var started = await _foregroundServiceController
            .StartAsync($"Wake phrase '{_settings.SelectedWakePhrase}' monitoring is active (scaffold).", cancellationToken)
            ;

        if (!started)
        {
            IsMonitoring = false;
            return;
        }

        try
        {
            await _wakeWordEngine.ConfigureAsync(_settings, cancellationToken);
            await _wakeWordEngine.StartAsync(cancellationToken);
            IsMonitoring = true;
        }
        catch
        {
            await _foregroundServiceController.StopAsync(cancellationToken);
            IsMonitoring = false;
            throw;
        }
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        try
        {
            await _wakeWordEngine.StopAsync(cancellationToken);
        }
        catch
        {
            // Best effort; still stop foreground service.
        }

        await _foregroundServiceController.StopAsync(cancellationToken);
        IsMonitoring = false;
    }

    public void SimulateWakeWordDetected(string? phrase = null)
    {
        ThrowIfDisposed();
        if (!IsMonitoring)
            return;

        if (_wakeWordEngine is AndroidWakeWordEngineStub stub)
        {
            stub.Simulate(string.IsNullOrWhiteSpace(phrase) ? _settings.SelectedWakePhrase : phrase);
            return;
        }

        var effectivePhrase = string.IsNullOrWhiteSpace(phrase) ? _settings.SelectedWakePhrase : phrase.Trim();
        WakeWordDetected?.Invoke(this, new AndroidWakeWordDetectedEventArgs(effectivePhrase, DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _foregroundServiceController.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup only.
        }

        try
        {
            _wakeWordEngine.WakeWordDetected -= OnWakeWordDetected;
            _wakeWordEngine.Dispose();
        }
        catch
        {
            // Best-effort cleanup only.
        }

        IsMonitoring = false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidWakeWordService));
    }

    private void OnWakeWordDetected(object? sender, AndroidWakeWordDetectedEventArgs e)
    {
        if (!IsMonitoring)
            return;

        WakeWordDetected?.Invoke(this, e);
    }

    private static AndroidWakeWordSettings NormalizeSettings(AndroidWakeWordSettings? settings)
    {
        if (settings == null)
            return new AndroidWakeWordSettings();

        return new AndroidWakeWordSettings
        {
            SelectedWakePhrase = NormalizeWakePhrase(settings.SelectedWakePhrase) ?? AndroidWakeWordCatalog.DefaultWakePhrase,
            Sensitivity = string.IsNullOrWhiteSpace(settings.Sensitivity) ? "medium" : settings.Sensitivity.Trim()
        };
    }

    private static string? NormalizeWakePhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return AndroidWakeWordCatalog.DefaultWakePhrase;

        return AndroidWakeWordCatalog.SupportedWakePhrases
            .FirstOrDefault(p => string.Equals(p, phrase.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Starts/stops the Android foreground service used for wake-word monitoring.
/// </summary>
public sealed class AndroidVoiceForegroundServiceController : IAndroidVoiceForegroundServiceController
{
    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("AndroidVoiceForegroundServiceController");

    public bool IsRunning { get; private set; }

    public async Task<bool> StartAsync(string? statusText = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = Application.Context;
        if (context == null)
        {
            Logger.LogWarning("Cannot start voice foreground service: Android application context is null.");
            return false;
        }

        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity != null)
        {
            var notificationPermission = await AndroidActivityHost
                .RequestPostNotificationsPermissionAsync(activity, cancellationToken)
                ;

            if (!notificationPermission)
            {
                Logger.LogWarning("Cannot start voice foreground service scaffold: notification permission denied.");
                IsRunning = false;
                return false;
            }
        }

        try
        {
            using var intent = AndroidWakeWordForegroundService.CreateStartIntent(context, statusText);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);

            IsRunning = true;
            Logger.LogInformation("Started voice foreground service scaffold.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to start voice foreground service scaffold.");
            IsRunning = false;
            return false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = Application.Context;
        if (context == null)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        try
        {
            using var intent = AndroidWakeWordForegroundService.CreateStopIntent(context);
            context.StartService(intent);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to request stop for voice foreground service scaffold.");
        }
        finally
        {
            IsRunning = false;
        }

        return Task.CompletedTask;
    }
}

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone, Name = "ninja.thesharp.mcpservermanager.voice.WakeWordForegroundService")]
public sealed class AndroidWakeWordForegroundService : Service
{
    public const string ActionStart = "ninja.thesharp.mcpservermanager.voice.START_WAKEWORD_FOREGROUND";
    public const string ActionStop = "ninja.thesharp.mcpservermanager.voice.STOP_WAKEWORD_FOREGROUND";
    public const string ExtraStatusText = "status_text";

    private const string ChannelId = "voice_wakeword_monitor";
    private const int NotificationId = 42001;

    public static Intent CreateStartIntent(Context context, string? statusText)
    {
        var intent = new Intent(context, typeof(AndroidWakeWordForegroundService));
        intent.SetAction(ActionStart);
        if (!string.IsNullOrWhiteSpace(statusText))
            intent.PutExtra(ExtraStatusText, statusText);
        return intent;
    }

    public static Intent CreateStopIntent(Context context)
    {
        var intent = new Intent(context, typeof(AndroidWakeWordForegroundService));
        intent.SetAction(ActionStop);
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

            var notificationManager = GetSystemService(Context.NotificationService) as NotificationManager;
            if (notificationManager != null)
                EnsureChannel(notificationManager);

            var statusText = intent?.GetStringExtra(ExtraStatusText);
            var notification = BuildNotification(statusText);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeMicrophone);
            else
                StartForeground(NotificationId, notification);
            return StartCommandResult.Sticky;
        }

        return AndroidCrashDiagnostics.ExecuteFatal(
            "AndroidWakeWordForegroundService.OnStartCommand",
            Core,
            "Wake-word foreground service crashed while processing a start/stop command.");
    }

    public override void OnDestroy()
    {
        void Core()
        {
            StopForegroundCompat();
            base.OnDestroy();
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "AndroidWakeWordForegroundService.OnDestroy",
            Core,
            "Wake-word foreground service crashed while shutting down.");
    }

    private Notification BuildNotification(string? statusText)
    {
        var contentText = string.IsNullOrWhiteSpace(statusText)
            ? "Wake phrase monitoring is active."
            : statusText;

        var pendingIntent = CreateLaunchPendingIntent();
        var builder = new Notification.Builder(this)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("Request Tracker Voice")
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

        return PendingIntent.GetActivity(this, 42002, launchIntent, flags);
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        if (manager.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(ChannelId, "Voice Monitoring", NotificationImportance.Low)
        {
            Description = "Foreground service used while wake phrase monitoring is enabled."
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
