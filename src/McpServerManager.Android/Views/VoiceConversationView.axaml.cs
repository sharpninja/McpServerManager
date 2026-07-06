using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using McpServerManager.Android.Services;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Android.Views;

public partial class VoiceConversationView : UserControl
{
    private IAndroidVoiceConversationController _controller = null!;
    private ComboBox? _wakePhraseComboBox;
    private CheckBox? _autoListenOnWakeCheckBox;
    private bool _isUpdatingWakePhraseSelector;
    private bool _isDisposed;

    /// <summary>Host composition sets this before any VoiceConversationView is instantiated.</summary>
    public static Func<IAndroidVoiceConversationController>? ControllerFactory { get; set; }

    public VoiceConversationView()
    {
        InitializeComponent();
        _controller = ControllerFactory?.Invoke()
            ?? throw new InvalidOperationException("ControllerFactory must be configured by Android host before view use");

        InitializeWakePhraseSelector();
        _controller.StatusChanged += OnControllerStatusChanged;
        _controller.WakeWordDetected += OnWakeWordDetected;
        _controller.SettingsChanged += OnVoiceChatSettingsChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private VoiceConversationViewModel? ViewModel => DataContext as VoiceConversationViewModel;

    protected void InitializeWakePhraseSelector()
    {
        _wakePhraseComboBox = this.FindControl<ComboBox>("WakePhraseComboBox");
        _autoListenOnWakeCheckBox = this.FindControl<CheckBox>("AutoListenOnWakeCheckBox");
        if (_wakePhraseComboBox == null)
            return;

        _isUpdatingWakePhraseSelector = true;
        try
        {
            var settings = _controller.LoadSettings();
            _wakePhraseComboBox.ItemsSource = _controller.AvailableWakePhrases;
            _wakePhraseComboBox.SelectedItem = settings.WakePhrase;
            if (_autoListenOnWakeCheckBox != null)
                _autoListenOnWakeCheckBox.IsChecked = settings.AutoListenOnWake;
        }
        finally
        {
            _isUpdatingWakePhraseSelector = false;
        }
    }

    protected async void OnListenFillInputClick(object? sender, RoutedEventArgs e)
    {
        await _controller.ListenAsync(ViewModel, submitAfterCapture: false).ConfigureAwait(true);
        e.Handled = true;
    }

    protected async void OnListenAndSendClick(object? sender, RoutedEventArgs e)
    {
        await _controller.ListenAsync(ViewModel, submitAfterCapture: true).ConfigureAwait(true);
        e.Handled = true;
    }

    protected void OnStopAudioClick(object? sender, RoutedEventArgs e)
    {
        _controller.StopAudioPlayback();
        if (ViewModel is { } vm)
            vm.StatusText = "Audio playback stopped.";
        e.Handled = true;
    }

    protected void OnInterruptClicked(object? sender, RoutedEventArgs e)
    {
        _controller.StopAudioPlayback();
    }

    protected async void OnSpeakReplyClick(object? sender, RoutedEventArgs e)
    {
        await _controller.SpeakReplyAsync(ViewModel).ConfigureAwait(true);
        e.Handled = true;
    }

    protected async void OnSaveTranscriptJsonlClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var vm = ViewModel;
        if (vm == null)
            return;

        try
        {
            await SaveTranscriptJsonlAsync(vm).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Save transcript failed: {ex.Message}";
        }
    }

    protected async Task SaveTranscriptJsonlAsync(VoiceConversationViewModel vm)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
        {
            vm.StatusText = "File save is not available.";
            return;
        }

        var jsonl = await vm.BuildTranscriptJsonLinesForExportAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(jsonl))
        {
            vm.StatusText = "No transcript entries available to save.";
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Voice Transcript",
            SuggestedFileName = vm.CreateTranscriptJsonlFileName(),
            DefaultExtension = "jsonl",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("JSON Lines") { Patterns = ["*.jsonl"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        }).ConfigureAwait(true);

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(jsonl).ConfigureAwait(true);
        vm.StatusText = $"Transcript JSONL saved: {file.Name}";
    }

    protected async void OnWakeStartClick(object? sender, RoutedEventArgs e)
    {
        await _controller.StartWakeWordAsync(ViewModel).ConfigureAwait(true);
        e.Handled = true;
    }

    protected void OnWakePhraseSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isDisposed || _isUpdatingWakePhraseSelector)
            return;

        var vm = ViewModel;
        var selectedPhrase = (sender as ComboBox)?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedPhrase))
            return;

        try
        {
            var updatedSettings = _controller.SaveWakePhrase(selectedPhrase);
            SyncControlsFromSettings(updatedSettings);
            if (vm != null)
                vm.StatusText = _controller.IsWakeMonitoring
                    ? $"Wake phrase set to '{updatedSettings.WakePhrase}' and applied to active monitoring."
                    : $"Wake phrase set to '{updatedSettings.WakePhrase}' (saved on device).";
        }
        catch (Exception ex)
        {
            SyncWakePhraseSelectorFromService();
            if (vm != null)
                vm.StatusText = $"Failed to save wake phrase: {ex.Message}";
        }

        e.Handled = true;
    }

    protected void OnAutoListenOnWakeCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isDisposed || _isUpdatingWakePhraseSelector)
            return;

        var vm = ViewModel;
        var isEnabled = (sender as CheckBox)?.IsChecked == true;
        var updatedSettings = _controller.SaveAutoListenOnWake(isEnabled);
        SyncControlsFromSettings(updatedSettings);
        if (vm != null)
            vm.StatusText = updatedSettings.AutoListenOnWake
                ? "Auto listen + send on wake enabled."
                : "Auto listen + send on wake disabled.";
        e.Handled = true;
    }

    protected async void OnWakeStopClick(object? sender, RoutedEventArgs e)
    {
        await _controller.StopWakeWordAsync(ViewModel).ConfigureAwait(true);
        e.Handled = true;
    }

    protected void OnSimWakeClick(object? sender, RoutedEventArgs e)
    {
        _controller.SimulateWakeWord(ViewModel);
        e.Handled = true;
    }

    protected void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
        _controller.StatusChanged -= OnControllerStatusChanged;
        _controller.WakeWordDetected -= OnWakeWordDetected;
        _controller.SettingsChanged -= OnVoiceChatSettingsChanged;
        _controller.Dispose();
    }

    protected void OnWakeWordDetected(object? sender, AndroidWakeWordDetectedEventArgs e)
    {
        UiDispatcherHost.Post(() => _ = HandleWakeWordDetectedOnUiAsync(e));
    }

    protected void SyncWakePhraseSelectorFromService()
    {
        SyncControlsFromSettings(_controller.LoadSettings());
    }

    protected void SyncControlsFromSettings(VoiceChatSettings settings)
    {
        if (_wakePhraseComboBox == null)
            return;

        _isUpdatingWakePhraseSelector = true;
        try
        {
            _wakePhraseComboBox.SelectedItem = settings.WakePhrase;
            if (_autoListenOnWakeCheckBox != null)
                _autoListenOnWakeCheckBox.IsChecked = settings.AutoListenOnWake;
        }
        finally
        {
            _isUpdatingWakePhraseSelector = false;
        }
    }

    protected void OnVoiceChatSettingsChanged(VoiceChatSettings settings)
    {
        UiDispatcherHost.Post(() => _ = ApplyVoiceChatSettingsAsync(settings));
    }

    protected async Task ApplyVoiceChatSettingsAsync(VoiceChatSettings settings)
    {
        if (_isDisposed)
            return;

        SyncControlsFromSettings(settings);
        await _controller.ApplyVoiceChatSettingsAsync(ViewModel, settings).ConfigureAwait(true);
    }

    protected async Task HandleWakeWordDetectedOnUiAsync(AndroidWakeWordDetectedEventArgs e)
    {
        if (_isDisposed)
            return;

        var autoListenAndSend = _autoListenOnWakeCheckBox?.IsChecked == true;
        await _controller.HandleWakeWordDetectedAsync(ViewModel, autoListenAndSend, e).ConfigureAwait(true);
    }

    protected void OnControllerStatusChanged(string status)
    {
        if (ViewModel is { } vm)
            vm.StatusText = status;
    }
}
