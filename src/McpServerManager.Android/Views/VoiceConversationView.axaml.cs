using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Android.Services;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class VoiceConversationView : UserControl
{
    private readonly IAndroidSpeechRecognitionService _speechRecognitionService = new AndroidSpeechRecognitionService();
    private readonly IAndroidTextToSpeechService _textToSpeechService = new AndroidTextToSpeechService();
    private readonly IAndroidAudioFocusService _audioFocusService = new AndroidAudioFocusService();
    private readonly IAndroidWakeWordService _wakeWordService = new AndroidWakeWordService();
    private ComboBox? _wakePhraseComboBox;
    private CheckBox? _autoListenOnWakeCheckBox;
    private IDisposable? _playbackFocusLease;
    private bool _isListening;
    private bool _isUpdatingWakePhraseSelector;
    private bool _isWakeAutoTurnRunning;
    private bool _isDisposed;

    public VoiceConversationView()
    {
        InitializeComponent();
        InitializeWakePhraseSelector();
        _audioFocusService.AudioFocusChanged += OnAudioFocusChanged;
        _wakeWordService.WakeWordDetected += OnWakeWordDetected;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private VoiceConversationViewModel? ViewModel => DataContext as VoiceConversationViewModel;

    private void InitializeWakePhraseSelector()
    {
        _wakePhraseComboBox = this.FindControl<ComboBox>("WakePhraseComboBox");
        _autoListenOnWakeCheckBox = this.FindControl<CheckBox>("AutoListenOnWakeCheckBox");
        if (_wakePhraseComboBox == null)
            return;

        _isUpdatingWakePhraseSelector = true;
        try
        {
            _wakePhraseComboBox.ItemsSource = _wakeWordService.AvailableWakePhrases;
            _wakePhraseComboBox.SelectedItem = _wakeWordService.SelectedWakePhrase;
        }
        finally
        {
            _isUpdatingWakePhraseSelector = false;
        }
    }

    private async void OnListenFillInputClick(object? sender, RoutedEventArgs e)
    {
        await ListenAsync(submitAfterCapture: false);
        e.Handled = true;
    }

    private async void OnListenAndSendClick(object? sender, RoutedEventArgs e)
    {
        await ListenAsync(submitAfterCapture: true);
        e.Handled = true;
    }

    private void OnStopAudioClick(object? sender, RoutedEventArgs e)
    {
        StopAudioPlayback();
        if (ViewModel is { } vm)
            vm.StatusText = "Audio playback stopped.";
        e.Handled = true;
    }

    private void OnInterruptClicked(object? sender, RoutedEventArgs e)
    {
        StopAudioPlayback();
    }

    private async void OnSpeakReplyClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null)
        {
            e.Handled = true;
            return;
        }

        var speakText = string.IsNullOrWhiteSpace(vm.AssistantSpeakText)
            ? vm.AssistantDisplayText
            : vm.AssistantSpeakText;

        if (string.IsNullOrWhiteSpace(speakText))
        {
            vm.StatusText = "No assistant reply is available to speak.";
            e.Handled = true;
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

        e.Handled = true;
    }

    private async void OnWakeStartClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        try
        {
            await _wakeWordService.StartMonitoringAsync().ConfigureAwait(true);
            if (vm != null)
                vm.StatusText = _wakeWordService.IsMonitoring
                    ? $"Wake-word scaffold monitoring started (phrase: {_wakeWordService.SelectedWakePhrase})."
                    : "Wake-word scaffold start was blocked (check notification permission).";
        }
        catch (Exception ex)
        {
            if (vm != null)
                vm.StatusText = $"Wake-word scaffold start failed: {ex.Message}";
        }

        e.Handled = true;
    }

    private async void OnWakePhraseSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isDisposed || _isUpdatingWakePhraseSelector)
            return;

        var vm = ViewModel;
        var selectedPhrase = (sender as ComboBox)?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedPhrase))
            return;

        try
        {
            var updated = await _wakeWordService.SetSelectedWakePhraseAsync(selectedPhrase).ConfigureAwait(true);
            if (!updated)
            {
                SyncWakePhraseSelectorFromService();
                if (vm != null)
                    vm.StatusText = "Unsupported wake phrase selection.";
                return;
            }

            SyncWakePhraseSelectorFromService();
            if (vm != null)
                vm.StatusText = _wakeWordService.IsMonitoring
                    ? $"Wake phrase set to '{_wakeWordService.SelectedWakePhrase}' and applied to active monitoring."
                    : $"Wake phrase set to '{_wakeWordService.SelectedWakePhrase}' (saved on device).";
        }
        catch (Exception ex)
        {
            SyncWakePhraseSelectorFromService();
            if (vm != null)
                vm.StatusText = $"Failed to save wake phrase: {ex.Message}";
        }

        e.Handled = true;
    }

    private async void OnWakeStopClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        try
        {
            await _wakeWordService.StopMonitoringAsync().ConfigureAwait(true);
            if (vm != null)
                vm.StatusText = "Wake-word scaffold monitoring stopped.";
        }
        catch (Exception ex)
        {
            if (vm != null)
                vm.StatusText = $"Wake-word scaffold stop failed: {ex.Message}";
        }

        e.Handled = true;
    }

    private void OnSimWakeClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        try
        {
            _wakeWordService.SimulateWakeWordDetected();
            if (vm != null && !_wakeWordService.IsMonitoring)
                vm.StatusText = "Wake-word scaffold is not running. Start it first.";
        }
        catch (Exception ex)
        {
            if (vm != null)
                vm.StatusText = $"Simulated wake failed: {ex.Message}";
        }

        e.Handled = true;
    }

    private async Task ListenAsync(bool submitAfterCapture)
    {
        if (_isListening)
            return;

        var vm = ViewModel;
        if (vm == null)
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

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
        _audioFocusService.AudioFocusChanged -= OnAudioFocusChanged;
        _wakeWordService.WakeWordDetected -= OnWakeWordDetected;
        StopAudioPlayback();
        _wakeWordService.Dispose();
        _audioFocusService.Dispose();
        _textToSpeechService.Dispose();
        _speechRecognitionService.Dispose();
    }

    private void OnAudioFocusChanged(object? sender, AndroidAudioFocusChangedEventArgs e)
    {
        if (!e.ShouldStopSpeechPlayback)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed || _playbackFocusLease == null)
                return;

            StopAudioPlayback();
            if (ViewModel is { } vm)
                vm.StatusText = "Audio focus lost. Stopped playback.";
        });
    }

    private void OnWakeWordDetected(object? sender, AndroidWakeWordDetectedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _ = HandleWakeWordDetectedOnUiAsync(e));
    }

    private void StopAudioPlayback()
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

    private void ReleasePlaybackFocusLease()
    {
        _playbackFocusLease?.Dispose();
        _playbackFocusLease = null;
    }

    private void SyncWakePhraseSelectorFromService()
    {
        if (_wakePhraseComboBox == null)
            return;

        _isUpdatingWakePhraseSelector = true;
        try
        {
            _wakePhraseComboBox.SelectedItem = _wakeWordService.SelectedWakePhrase;
        }
        finally
        {
            _isUpdatingWakePhraseSelector = false;
        }
    }

    private async Task HandleWakeWordDetectedOnUiAsync(AndroidWakeWordDetectedEventArgs e)
    {
        if (_isDisposed)
            return;

        var vm = ViewModel;
        if (vm == null)
            return;

        var autoListenAndSend = _autoListenOnWakeCheckBox?.IsChecked == true;
        if (!autoListenAndSend)
        {
            vm.StatusText = $"Wake phrase detected ({e.Phrase}) at {e.DetectedAtUtc:HH:mm:ss} UTC.";
            return;
        }

        if (_isWakeAutoTurnRunning || _isListening || vm.IsBusy)
        {
            vm.StatusText = $"Wake phrase detected ({e.Phrase}) but voice turn is already in progress.";
            return;
        }

        _isWakeAutoTurnRunning = true;
        try
        {
            vm.StatusText = $"Wake phrase detected ({e.Phrase}). Starting listen + send...";
            await ListenAsync(submitAfterCapture: true).ConfigureAwait(true);
        }
        finally
        {
            _isWakeAutoTurnRunning = false;
        }
    }
}
