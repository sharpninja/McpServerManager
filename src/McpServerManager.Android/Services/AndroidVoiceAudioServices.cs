using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Speech;
using Android.Speech.Tts;
using Java.Util;

namespace McpServerManager.Android.Services;

public enum AndroidVoiceAudioFocusUsage
{
    SpeechRecognition,
    TextToSpeechPlayback
}

public sealed class AndroidAudioFocusChangedEventArgs(AudioFocus focusChange) : EventArgs
{
    public AudioFocus FocusChange { get; } = focusChange;

    public bool ShouldStopSpeechPlayback =>
        FocusChange == AudioFocus.Loss ||
        FocusChange == AudioFocus.LossTransient ||
        FocusChange == AudioFocus.LossTransientCanDuck;
}

public interface IAndroidSpeechRecognitionService : IDisposable
{
    Task<string> RecognizeOnceAsync(string? languageTag, CancellationToken cancellationToken = default);
    void CancelActiveRecognition();
}

public interface IAndroidTextToSpeechService : IDisposable
{
    Task SpeakAsync(string text, string? languageTag, CancellationToken cancellationToken = default);
    void Stop();
}

public interface IAndroidAudioFocusService : IDisposable
{
    event EventHandler<AndroidAudioFocusChangedEventArgs>? AudioFocusChanged;
    IDisposable Acquire(AndroidVoiceAudioFocusUsage usage);
}

/// <summary>
/// Minimal Android audio focus coordinator for the voice harness.
/// </summary>
public sealed class AndroidAudioFocusService : IAndroidAudioFocusService
{
    private readonly object _sync = new();
    private readonly FocusChangeListener _listener;
    private int _activeLeaseCount;
    private bool _disposed;

    public event EventHandler<AndroidAudioFocusChangedEventArgs>? AudioFocusChanged;

    public AndroidAudioFocusService()
    {
        _listener = new FocusChangeListener(this);
    }

    public IDisposable Acquire(AndroidVoiceAudioFocusUsage usage)
    {
        ThrowIfDisposed();

        var activity = AndroidActivityHost.TryGetCurrentActivity();
        var context = activity?.ApplicationContext ?? Application.Context;
        var audioManager = context?.GetSystemService(Context.AudioService) as AudioManager;
        if (audioManager == null)
            return NoopDisposable.Instance;

        lock (_sync)
        {
            ThrowIfDisposed();
            _activeLeaseCount++;
        }

        try
        {
            var durationHint = usage == AndroidVoiceAudioFocusUsage.TextToSpeechPlayback
                ? AudioFocus.GainTransientMayDuck
                : AudioFocus.GainTransient;

            _ = audioManager.RequestAudioFocus(_listener, Stream.Music, durationHint);
            return new AudioFocusLease(this, audioManager);
        }
        catch
        {
            ReleaseLeaseInternal(audioManager);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        // Any outstanding lease disposal will no-op after count reaches zero.
        _activeLeaseCount = 0;
    }

    private void ReleaseLeaseInternal(AudioManager audioManager)
    {
        lock (_sync)
        {
            if (_activeLeaseCount <= 0)
                return;

            _activeLeaseCount--;
            if (_activeLeaseCount > 0)
                return;
        }

        try
        {
            _ = audioManager.AbandonAudioFocus(_listener);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidAudioFocusService));
    }

    private void OnAudioFocusChanged(AudioFocus focusChange)
    {
        AudioFocusChanged?.Invoke(this, new AndroidAudioFocusChangedEventArgs(focusChange));
    }

    private sealed class AudioFocusLease(AndroidAudioFocusService owner, AudioManager audioManager) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            owner.ReleaseLeaseInternal(audioManager);
        }
    }

    private sealed class FocusChangeListener(AndroidAudioFocusService owner)
        : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            owner.OnAudioFocusChanged(focusChange);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>
/// One-shot native Android speech recognition wrapper for the voice integration harness.
/// </summary>
public sealed class AndroidSpeechRecognitionService : IAndroidSpeechRecognitionService
{
    private readonly object _sync = new();
    private SpeechRecognizer? _recognizer;
    private RecognitionListenerProxy? _listener;
    private TaskCompletionSource<string>? _activeRecognition;
    private bool _disposed;

    public async Task<string> RecognizeOnceAsync(string? languageTag, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var activity = AndroidActivityHost.TryGetCurrentActivity()
            ?? throw new InvalidOperationException("Android activity is not available.");

        var permissionGranted = await AndroidActivityHost.RequestRecordAudioPermissionAsync(activity, cancellationToken)
            .ConfigureAwait(false);
        if (!permissionGranted)
            throw new InvalidOperationException("Microphone permission was denied.");

        if (!SpeechRecognizer.IsRecognitionAvailable(activity))
            throw new InvalidOperationException("Speech recognition is not available on this device.");

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            if (_activeRecognition is { Task.IsCompleted: false })
                throw new InvalidOperationException("Speech recognition is already in progress.");
            _activeRecognition = tcs;
        }

        try
        {
            await RunOnUiThreadAsync(activity, () =>
            {
                _listener ??= new RecognitionListenerProxy(this);
                var recognizer = _recognizer ??=
                    SpeechRecognizer.CreateSpeechRecognizer(activity)
                    ?? throw new InvalidOperationException("Failed to create Android speech recognizer.");
                recognizer.SetRecognitionListener(_listener);

                using var intent = BuildRecognizerIntent(activity, languageTag);
                recognizer.StartListening(intent);
            }).ConfigureAwait(false);

            using var _ = cancellationToken.Register(() =>
            {
                try
                {
                    CancelActiveRecognition();
                }
                catch
                {
                    // Best-effort cancellation only.
                }
            });

            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryClearActiveRecognition(tcs);
            throw;
        }
    }

    public void CancelActiveRecognition()
    {
        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null)
        {
            _recognizer?.Cancel();
            return;
        }

        if (Looper.MyLooper() == Looper.MainLooper)
        {
            _recognizer?.Cancel();
            return;
        }

        activity.RunOnUiThread(() => _recognizer?.Cancel());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            CancelActiveRecognition();
        }
        catch
        {
            // Ignore cleanup failures.
        }

        var recognizer = _recognizer;
        _recognizer = null;
        _listener = null;

        if (recognizer != null)
        {
            try
            {
                recognizer.Destroy();
                recognizer.Dispose();
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        lock (_sync)
        {
            _activeRecognition?.TrySetCanceled();
            _activeRecognition = null;
        }
    }

    private void OnRecognitionResults(IList<string>? results)
    {
        var best = results?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? string.Empty;
        CompleteActiveRecognition(best);
    }

    private void OnRecognitionError(SpeechRecognizerError error)
    {
        switch (error)
        {
            case SpeechRecognizerError.NoMatch:
            case SpeechRecognizerError.SpeechTimeout:
                CompleteActiveRecognition(string.Empty);
                return;
            case SpeechRecognizerError.Client:
                FailActiveRecognition("Speech recognition was canceled.");
                return;
            default:
                FailActiveRecognition($"Speech recognition error: {error}.");
                return;
        }
    }

    private void CompleteActiveRecognition(string text)
    {
        TaskCompletionSource<string>? tcs;
        lock (_sync)
        {
            tcs = _activeRecognition;
            _activeRecognition = null;
        }

        tcs?.TrySetResult(text);
    }

    private void FailActiveRecognition(string message)
    {
        TaskCompletionSource<string>? tcs;
        lock (_sync)
        {
            tcs = _activeRecognition;
            _activeRecognition = null;
        }

        tcs?.TrySetException(new InvalidOperationException(message));
    }

    private void TryClearActiveRecognition(TaskCompletionSource<string> candidate)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_activeRecognition, candidate))
                _activeRecognition = null;
        }
    }

    private static Intent BuildRecognizerIntent(Activity activity, string? languageTag)
    {
        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraCallingPackage, activity.PackageName);
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 3);

        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            intent.PutExtra(RecognizerIntent.ExtraLanguage, languageTag);
            intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, languageTag);
        }

        return intent;
    }

    private static Task RunOnUiThreadAsync(Activity activity, Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidSpeechRecognitionService));
    }

    private sealed class RecognitionListenerProxy(AndroidSpeechRecognitionService owner)
        : Java.Lang.Object, IRecognitionListener
    {
        public void OnBeginningOfSpeech()
        {
        }

        public void OnBufferReceived(byte[]? buffer)
        {
        }

        public void OnEndOfSpeech()
        {
        }

        public void OnError(SpeechRecognizerError error)
        {
            owner.OnRecognitionError(error);
        }

        public void OnEvent(int eventType, Bundle? @params)
        {
        }

        public void OnPartialResults(Bundle? partialResults)
        {
        }

        public void OnReadyForSpeech(Bundle? @params)
        {
        }

        public void OnResults(Bundle? results)
        {
            var list = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            owner.OnRecognitionResults(list);
        }

        public void OnRmsChanged(float rmsdB)
        {
        }
    }
}

/// <summary>
/// Native Android text-to-speech wrapper for voice reply playback in the Android harness UI.
/// </summary>
public sealed class AndroidTextToSpeechService : IAndroidTextToSpeechService
{
    private readonly object _sync = new();
    private TextToSpeech? _tts;
    private Task<bool>? _initTask;
    private bool _disposed;

    public async Task SpeakAsync(string text, string? languageTag, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(text))
            return;

        var activity = AndroidActivityHost.TryGetCurrentActivity()
            ?? throw new InvalidOperationException("Android activity is not available.");

        var initialized = await EnsureInitializedAsync(activity, cancellationToken).ConfigureAwait(false);
        if (!initialized)
            throw new InvalidOperationException("Android text-to-speech initialization failed.");

        await RunOnUiThreadAsync(activity, () =>
        {
            var tts = _tts ?? throw new InvalidOperationException("Android text-to-speech is unavailable.");
            if (!string.IsNullOrWhiteSpace(languageTag))
            {
                try
                {
                    tts.SetLanguage(Locale.ForLanguageTag(languageTag));
                }
                catch
                {
                    tts.SetLanguage(Locale.Default);
                }
            }

            tts.Speak(text, QueueMode.Flush, null, $"voice-{Guid.NewGuid():N}");
        }).ConfigureAwait(false);
    }

    public void Stop()
    {
        var tts = _tts;
        if (tts == null)
            return;

        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null || Looper.MyLooper() == Looper.MainLooper)
        {
            try
            {
                tts.Stop();
            }
            catch
            {
                // Ignore stop errors.
            }
            return;
        }

        activity.RunOnUiThread(() =>
        {
            try
            {
                tts.Stop();
            }
            catch
            {
                // Ignore stop errors.
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            Stop();
        }
        catch
        {
            // Ignore cleanup failures.
        }

        var tts = _tts;
        _tts = null;
        _initTask = null;
        if (tts == null)
            return;

        try
        {
            tts.Shutdown();
            tts.Dispose();
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private Task<bool> EnsureInitializedAsync(Activity activity, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _initTask ??= CreateAndInitializeAsync(activity, cancellationToken);
            return _initTask;
        }
    }

    private Task<bool> CreateAndInitializeAsync(Activity activity, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        _ = RunOnUiThreadAsync(activity, () =>
        {
            if (_disposed)
            {
                tcs.TrySetCanceled();
                return;
            }

            _tts?.Dispose();
            _tts = new TextToSpeech(activity.ApplicationContext, new TtsInitListener(tcs));
        }).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                if (task.Exception is { } aggregate)
                    tcs.TrySetException(aggregate.InnerExceptions);
                else
                    tcs.TrySetException(new InvalidOperationException("TTS init failed."));
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return tcs.Task;
    }

    private static Task RunOnUiThreadAsync(Activity activity, Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidTextToSpeechService));
    }

    private sealed class TtsInitListener(TaskCompletionSource<bool> tcs) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status)
        {
            tcs.TrySetResult(status == OperationResult.Success);
        }
    }
}
