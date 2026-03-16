using Android.App;
using Android.Content;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

public sealed class AndroidVoiceChatSettingsStore : IVoiceChatSettingsStore
{
    private const string PreferencesName = "McpServerManager.Voice";
    private const string LanguageKey = "VoiceLanguage";
    private const string AutoContinueKey = "AutoContinueEnabled";
    private const string WakePhraseKey = "WakePhrase";
    private const string WakeSensitivityKey = "WakeSensitivity";
    private const string AutoListenOnWakeKey = "AutoListenOnWake";
    private const string PicovoiceAccessKeyKey = "PicovoiceAccessKey";

    public bool SupportsWakeWordSettings => true;

    public VoiceChatSettings Load()
    {
        try
        {
            var prefs = GetPreferences();
            return VoiceChatSettingsService.Normalize(new VoiceChatSettings
            {
                Language = prefs?.GetString(LanguageKey, null) ?? VoiceChatSettingsService.DefaultLanguage,
                AutoContinueEnabled = prefs?.GetBoolean(AutoContinueKey, true) ?? true,
                WakePhrase = prefs?.GetString(WakePhraseKey, null) ?? VoiceChatSettingsService.DefaultWakePhrase,
                WakeSensitivity = prefs?.GetString(WakeSensitivityKey, null) ?? VoiceChatSettingsService.DefaultWakeSensitivity,
                AutoListenOnWake = prefs?.GetBoolean(AutoListenOnWakeKey, true) ?? true,
                PicovoiceAccessKey = prefs?.GetString(PicovoiceAccessKeyKey, null) ?? string.Empty
            });
        }
        catch
        {
            return new VoiceChatSettings();
        }
    }

    public void Save(VoiceChatSettings settings)
    {
        var normalized = VoiceChatSettingsService.Normalize(settings);
        var editor = GetPreferences()?.Edit();
        if (editor == null)
            return;

        using (editor)
        {
            editor.PutString(LanguageKey, normalized.Language);
            editor.PutBoolean(AutoContinueKey, normalized.AutoContinueEnabled);
            editor.PutString(WakePhraseKey, normalized.WakePhrase);
            editor.PutString(WakeSensitivityKey, normalized.WakeSensitivity);
            editor.PutBoolean(AutoListenOnWakeKey, normalized.AutoListenOnWake);
            if (string.IsNullOrWhiteSpace(normalized.PicovoiceAccessKey))
                editor.Remove(PicovoiceAccessKeyKey);
            else
                editor.PutString(PicovoiceAccessKeyKey, normalized.PicovoiceAccessKey);
            editor.Apply();
        }
    }

    private static ISharedPreferences? GetPreferences()
    {
        var context = Application.Context;
        return context?.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }
}
