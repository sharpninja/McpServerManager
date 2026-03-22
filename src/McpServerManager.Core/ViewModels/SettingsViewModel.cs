using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

/// <summary>App wrapper for UI.Core settings ViewModel.</summary>
public partial class SettingsViewModel : McpServer.UI.Core.ViewModels.SettingsViewModel
{
    private readonly CqrsDispatcher _dispatcher;
    private readonly VoiceChatSettingsService _voiceChatSettingsService;

    [ObservableProperty]
    private string _voiceLanguage = VoiceChatSettingsService.DefaultLanguage;

    [ObservableProperty]
    private bool _autoContinueEnabled = true;

    [ObservableProperty]
    private string _wakePhrase = VoiceChatSettingsService.DefaultWakePhrase;

    [ObservableProperty]
    private string _wakeSensitivity = VoiceChatSettingsService.DefaultWakeSensitivity;

    [ObservableProperty]
    private bool _autoListenOnWake = true;

    [ObservableProperty]
    private string _picovoiceAccessKey = string.Empty;

    public IReadOnlyList<string> AvailableWakePhrases => _voiceChatSettingsService.AvailableWakePhrases;
    public IReadOnlyList<string> AvailableWakeSensitivities => _voiceChatSettingsService.AvailableWakeSensitivities;
    public bool SupportsWakeWordSettings => _voiceChatSettingsService.SupportsWakeWordSettings;

    public SettingsViewModel(
        CqrsDispatcher dispatcher,
        McpServer.UI.Core.Services.ISpeechFilterService? speechFilterService = null)
        : base(speechFilterService ?? new SpeechFilterServiceAdapter())
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _voiceChatSettingsService = VoiceChatSettingsService.Instance;
        ApplyVoiceChatSettings(_voiceChatSettingsService.Load());
    }

    private void SaveSettings()
    {
        SaveFilterWords();
        ApplyVoiceChatSettings(_voiceChatSettingsService.Save(new VoiceChatSettings
        {
            Language = VoiceLanguage,
            AutoContinueEnabled = AutoContinueEnabled,
            WakePhrase = WakePhrase,
            WakeSensitivity = WakeSensitivity,
            AutoListenOnWake = AutoListenOnWake,
            PicovoiceAccessKey = PicovoiceAccessKey
        }));
        StatusMessage = "Saved speech filter and voice chat settings.";
    }

    private void RevertSettings()
    {
        RevertFilterWords();
        ApplyVoiceChatSettings(_voiceChatSettingsService.Load());
        StatusMessage = "Reverted to saved values.";
    }

    private void ApplyVoiceChatSettings(VoiceChatSettings settings)
    {
        var normalized = VoiceChatSettingsService.Normalize(settings);
        VoiceLanguage = normalized.Language;
        AutoContinueEnabled = normalized.AutoContinueEnabled;
        WakePhrase = normalized.WakePhrase;
        WakeSensitivity = normalized.WakeSensitivity;
        AutoListenOnWake = normalized.AutoListenOnWake;
        PicovoiceAccessKey = normalized.PicovoiceAccessKey;
        OnPropertyChanged(nameof(SupportsWakeWordSettings));
    }
}
