using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using AndroidOS = Android.OS;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Android.Services;

public interface IAndroidVoiceConversationController : IDisposable
{
    IReadOnlyList<string> AvailableWakePhrases { get; }
    bool IsWakeMonitoring { get; }
    event Action<VoiceChatSettings>? SettingsChanged;
    event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;
    event Action<string>? StatusChanged;
    VoiceChatSettings LoadSettings();
    VoiceChatSettings SaveWakePhrase(string selectedPhrase);
    VoiceChatSettings SaveAutoListenOnWake(bool isEnabled);
    Task ApplyVoiceChatSettingsAsync(VoiceConversationViewModel? vm, VoiceChatSettings settings);
    Task ListenAsync(VoiceConversationViewModel? vm, bool submitAfterCapture);
    Task SpeakReplyAsync(VoiceConversationViewModel? vm);
    Task StartWakeWordAsync(VoiceConversationViewModel? vm);
    Task StopWakeWordAsync(VoiceConversationViewModel? vm);
    void SimulateWakeWord(VoiceConversationViewModel? vm);
    Task HandleWakeWordDetectedAsync(VoiceConversationViewModel? vm, bool autoListenAndSend, AndroidWakeWordDetectedEventArgs args);
    void StopAudioPlayback();
}

public sealed class AndroidVoiceConversationController : IAndroidVoiceConversationController
{
    private readonly VoiceChatSettingsService _settingsService = VoiceChatSettingsService.Instance;
    private readonly IAndroidSpeechRecognitionService _speechRecognitionService;
    private readonly IAndroidTextToSpeechService _textToSpeechService;
    private readonly IAndroidAudioFocusService _audioFocusService;
    private readonly IAndroidWakeWordService _wakeWordService;
    private IDisposable? _playbackFocusLease;
    private bool _isListening;
    private bool _isWakeAutoTurnRunning;
    private bool _isDisposed;

    public IReadOnlyList<string> AvailableWakePhrases => _wakeWordService.AvailableWakePhrases;
    public bool IsWakeMonitoring => _wakeWordService.IsMonitoring;

    public event Action<VoiceChatSettings>? SettingsChanged;
    public event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;
    public event Action<string>? StatusChanged;

    public AndroidVoiceConversationController(
        IAndroidSpeechRecognitionService speechRecognitionService,
        IAndroidTextToSpeechService textToSpeechService,
        IAndroidAudioFocusService audioFocusService,
        IAndroidWakeWordService wakeWordService)
    {
        _speechRecognitionService = speechRecognitionService;
        _textToSpeechService = textToSpeechService;
        _audioFocusService = audioFocusService;
        _wakeWordService = wakeWordService;

        _audioFocusService.AudioFocusChanged += OnAudioFocusChanged;
        _wakeWordService.WakeWordDetected += OnWakeWordDetected;
        _settingsService.SettingsChanged += OnVoiceChatSettingsChanged;
    }

    public VoiceChatSettings LoadSettings() => _settingsService.Load();

    public VoiceChatSettings SaveWakePhrase(string selectedPhrase)
    {
        return SaveSettings(current => new VoiceChatSettings
        {
            Language = current.Language,
            AutoContinueEnabled = current.AutoContinueEnabled,
            WakePhrase = selectedPhrase,
            WakeSensitivity = current.WakeSensitivity,
            AutoListenOnWake = current.AutoListenOnWake,
            PicovoiceAccessKey = current.PicovoiceAccessKey
        });
    }

    public VoiceChatSettings SaveAutoListenOnWake(bool isEnabled)
    {
        return SaveSettings(current => new VoiceChatSettings
        {
            Language = current.Language,
            AutoContinueEnabled = current.AutoContinueEnabled,
            WakePhrase = current.WakePhrase,
            WakeSensitivity = current.WakeSensitivity,
            AutoListenOnWake = isEnabled,
            PicovoiceAccessKey = current.PicovoiceAccessKey
        });
    }

    public async Task ApplyVoiceChatSettingsAsync(VoiceConversationViewModel? vm, VoiceChatSettings settings)
    {
        if (_isDisposed)
            return;

        try
        {
            await _wakeWordService.ApplySettingsAsync(new AndroidWakeWordSettings
            {
                SelectedWakePhrase = settings.WakePhrase,
                Sensitivity = settings.WakeSensitivity
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatus(vm, $"Failed to apply wake-word settings: {ex.Message}");
        }
    }

    public async Task ListenAsync(VoiceConversationViewModel? vm, bool submitAfterCapture)
    {
        if (_isListening || vm == null)
            return;

        _isListening = true;
        try
        {
            StopAudioPlayback();
            using var _ = _audioFocusService.Acquire(AndroidVoiceAudioFocusUsage.SpeechRecognition);
            vm.StatusText = "Listening (Android STT)...";

            var transcript = await _speechRecognitionService.RecognizeOnceAsync(vm.Language).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                vm.StatusText = "No speech recognized. Try again.";
                return;
            }

            vm.TranscriptInput = transcript.Trim();
            vm.StatusText = "Transcript captured from microphone.";

            if (submitAfterCapture)
            {
                vm.StatusText = "Submitting recognized transcript...";
                await vm.SubmitTurnCommand.ExecuteAsync(null).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Speech recognition failed: {ex.Message}";
        }
        finally
        {
            _isListening = false;
        }
    }

    public async Task SpeakReplyAsync(VoiceConversationViewModel? vm)
    {
        if (vm == null)
            return;

        var speakText = string.IsNullOrWhiteSpace(vm.AssistantSpeakText)
            ? vm.AssistantDisplayText
            : vm.AssistantSpeakText;

        if (string.IsNullOrWhiteSpace(speakText))
        {
            vm.StatusText = "No assistant reply is available to speak.";
            return;
        }

        try
        {
            StopAudioPlayback();
            _playbackFocusLease = _audioFocusService.Acquire(AndroidVoiceAudioFocusUsage.TextToSpeechPlayback);
            vm.StatusText = "Starting Android TTS playback...";
            await _textToSpeechService.SpeakAsync(speakText, vm.Language).ConfigureAwait(true);
            vm.StatusText = "Playing assistant reply.";
        }
        catch (Exception ex)
        {
            ReleasePlaybackFocusLease();
            vm.StatusText = $"TTS playback failed: {ex.Message}";
        }
    }

    public async Task StartWakeWordAsync(VoiceConversationViewModel? vm)
    {
        try
        {
            await _wakeWordService.StartMonitoringAsync().ConfigureAwait(true);
            SetStatus(vm, _wakeWordService.IsMonitoring
                ? $"Wake-word scaffold monitoring started (phrase: {_wakeWordService.SelectedWakePhrase})."
                : "Wake-word scaffold start was blocked (check notification permission).");
        }
        catch (Exception ex)
        {
            SetStatus(vm, $"Wake-word scaffold start failed: {ex.Message}");
        }
    }

    public async Task StopWakeWordAsync(VoiceConversationViewModel? vm)
    {
        try
        {
            await _wakeWordService.StopMonitoringAsync().ConfigureAwait(true);
            SetStatus(vm, "Wake-word scaffold monitoring stopped.");
        }
        catch (Exception ex)
        {
            SetStatus(vm, $"Wake-word scaffold stop failed: {ex.Message}");
        }
    }

    public void SimulateWakeWord(VoiceConversationViewModel? vm)
    {
        try
        {
            _wakeWordService.SimulateWakeWordDetected();
            if (vm != null && !_wakeWordService.IsMonitoring)
                vm.StatusText = "Wake-word scaffold is not running. Start it first.";
        }
        catch (Exception ex)
        {
            SetStatus(vm, $"Simulated wake failed: {ex.Message}");
        }
    }

    public async Task HandleWakeWordDetectedAsync(VoiceConversationViewModel? vm, bool autoListenAndSend, AndroidWakeWordDetectedEventArgs args)
    {
        if (_isDisposed || vm == null)
            return;

        if (!autoListenAndSend)
        {
            vm.StatusText = $"Wake phrase detected ({args.Phrase}) at {args.DetectedAtUtc:HH:mm:ss} UTC.";
            return;
        }

        if (_isWakeAutoTurnRunning || _isListening || vm.IsBusy)
        {
            vm.StatusText = $"Wake phrase detected ({args.Phrase}) but voice turn is already in progress.";
            return;
        }

        _isWakeAutoTurnRunning = true;
        try
        {
            vm.StatusText = $"Wake phrase detected ({args.Phrase}). Starting listen + send...";
            await ListenAsync(vm, submitAfterCapture: true).ConfigureAwait(true);
        }
        finally
        {
            _isWakeAutoTurnRunning = false;
        }
    }

    public void StopAudioPlayback()
    {
        try
        {
            _textToSpeechService.Stop();
        }
        finally
        {
            ReleasePlaybackFocusLease();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _audioFocusService.AudioFocusChanged -= OnAudioFocusChanged;
        _wakeWordService.WakeWordDetected -= OnWakeWordDetected;
        _settingsService.SettingsChanged -= OnVoiceChatSettingsChanged;
        StopAudioPlayback();
        _ = StopWakeWordBestEffortAsync();
    }

    private VoiceChatSettings SaveSettings(Func<VoiceChatSettings, VoiceChatSettings> update)
    {
        return _settingsService.Save(update(_settingsService.Load()));
    }

    private void OnAudioFocusChanged(object? sender, AndroidAudioFocusChangedEventArgs e)
    {
        if (!e.ShouldStopSpeechPlayback)
            return;

        UiDispatcherHost.Post(() =>
        {
            if (_isDisposed || _playbackFocusLease == null)
                return;

            StopAudioPlayback();
            StatusChanged?.Invoke("Audio focus lost. Stopped playback.");
        });
    }

    private void OnWakeWordDetected(object? sender, AndroidWakeWordDetectedEventArgs e)
    {
        WakeWordDetected?.Invoke(this, e);
    }

    private void OnVoiceChatSettingsChanged(VoiceChatSettings settings)
    {
        SettingsChanged?.Invoke(settings);
    }

    private void ReleasePlaybackFocusLease()
    {
        _playbackFocusLease?.Dispose();
        _playbackFocusLease = null;
    }

    private void SetStatus(VoiceConversationViewModel? vm, string status)
    {
        if (vm != null)
            vm.StatusText = status;
    }

    private async Task StopWakeWordBestEffortAsync()
    {
        try
        {
            await _wakeWordService.StopMonitoringAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort lifecycle cleanup during view disposal.
        }
    }
}

public interface IAndroidSimplifiedVoiceController : IDisposable
{
    bool IsForegroundServiceRunning { get; }
    void StartForegroundService(string statusText);
    void StopForegroundService();
    void UpdateForegroundServiceStatus(string statusText);
    IDisposable AcquireSpeechRecognitionFocus();
    IDisposable AcquireTextToSpeechFocus();
    Task<string> RecognizeSpeechOnceAsync(string? language, CancellationToken cancellationToken);
    Task SpeakTextAsync(string text, string? language, CancellationToken cancellationToken);
    void StopTextToSpeech();
}

public sealed class AndroidSimplifiedVoiceController(
    IAndroidSpeechRecognitionService speechRecognitionService,
    IAndroidTextToSpeechService textToSpeechService,
    IAndroidAudioFocusService audioFocusService,
    ILogger<AndroidSimplifiedVoiceController> logger)
    : IAndroidSimplifiedVoiceController
{
    private bool _foregroundServiceRunning;

    public bool IsForegroundServiceRunning => _foregroundServiceRunning;

    public void StartForegroundService(string statusText)
    {
        try
        {
            var context = Application.Context;
            using var intent = VoiceSessionForegroundService.CreateStartIntent(context, statusText);
            if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
            _foregroundServiceRunning = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start voice foreground service");
        }
    }

    public void StopForegroundService()
    {
        if (!_foregroundServiceRunning)
            return;

        try
        {
            var context = Application.Context;
            using var intent = VoiceSessionForegroundService.CreateStopIntent(context);
            context.StartService(intent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop voice foreground service");
        }
        finally
        {
            _foregroundServiceRunning = false;
        }
    }

    public void UpdateForegroundServiceStatus(string statusText)
    {
        try
        {
            var context = Application.Context;
            using var intent = VoiceSessionForegroundService.CreateUpdateIntent(context, statusText);
            context.StartService(intent);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to update foreground service status");
        }
    }

    public IDisposable AcquireSpeechRecognitionFocus()
    {
        return audioFocusService.Acquire(AndroidVoiceAudioFocusUsage.SpeechRecognition);
    }

    public IDisposable AcquireTextToSpeechFocus()
    {
        return audioFocusService.Acquire(AndroidVoiceAudioFocusUsage.TextToSpeechPlayback);
    }

    public Task<string> RecognizeSpeechOnceAsync(string? language, CancellationToken cancellationToken)
    {
        return speechRecognitionService.RecognizeOnceAsync(language, cancellationToken);
    }

    public Task SpeakTextAsync(string text, string? language, CancellationToken cancellationToken)
    {
        return textToSpeechService.SpeakAsync(text, language, cancellationToken);
    }

    public void StopTextToSpeech()
    {
        textToSpeechService.Stop();
    }

    public void Dispose()
    {
        StopTextToSpeech();
        StopForegroundService();
    }
}
