using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace McpServerManager.Core.Services;

public sealed class VoiceChatSettings
{
    public string Language { get; init; } = VoiceChatSettingsService.DefaultLanguage;
    public bool AutoContinueEnabled { get; init; } = true;
    public string WakePhrase { get; init; } = VoiceChatSettingsService.DefaultWakePhrase;
    public string WakeSensitivity { get; init; } = VoiceChatSettingsService.DefaultWakeSensitivity;
    public bool AutoListenOnWake { get; init; } = true;
    public string PicovoiceAccessKey { get; init; } = string.Empty;
}

public interface IVoiceChatSettingsStore
{
    bool SupportsWakeWordSettings { get; }
    VoiceChatSettings Load();
    void Save(VoiceChatSettings settings);
}

public sealed class FileVoiceChatSettingsStore : IVoiceChatSettingsStore
{
    private const string SettingsFileName = "voice-chat-settings.json";

    public bool SupportsWakeWordSettings => false;

    public VoiceChatSettings Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new VoiceChatSettings();

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<VoiceChatSettings>(json);
            return VoiceChatSettingsService.Normalize(settings);
        }
        catch
        {
            return new VoiceChatSettings();
        }
    }

    public void Save(VoiceChatSettings settings)
    {
        var path = GetFilePath();
        var normalized = VoiceChatSettingsService.Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    private static string GetFilePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServerManager");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, SettingsFileName);
    }
}

public sealed class VoiceChatSettingsService
{
    public const string DefaultLanguage = "en-US";
    public const string DefaultWakePhrase = "Hey Tracker";
    public const string DefaultWakeSensitivity = "medium";

    private static readonly string[] WakePhrases =
    [
        "Hey Tracker",
        "Okay Tracker",
        "Hello Tracker"
    ];

    private static readonly string[] WakeSensitivities =
    [
        "low",
        "medium",
        "high"
    ];

    private readonly object _gate = new();
    private IVoiceChatSettingsStore _store = new FileVoiceChatSettingsStore();

    public static VoiceChatSettingsService Instance { get; } = new();

    private VoiceChatSettingsService()
    {
    }

    public event Action<VoiceChatSettings>? SettingsChanged;

    public IReadOnlyList<string> AvailableWakePhrases => WakePhrases;
    public IReadOnlyList<string> AvailableWakeSensitivities => WakeSensitivities;

    public bool SupportsWakeWordSettings
    {
        get
        {
            lock (_gate)
                return _store.SupportsWakeWordSettings;
        }
    }

    public void ConfigureStore(IVoiceChatSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        lock (_gate)
            _store = store;
    }

    public VoiceChatSettings Load()
    {
        IVoiceChatSettingsStore store;
        lock (_gate)
            store = _store;

        return Normalize(store.Load());
    }

    public VoiceChatSettings Save(VoiceChatSettings settings)
    {
        var normalized = Normalize(settings);
        IVoiceChatSettingsStore store;
        lock (_gate)
            store = _store;

        store.Save(normalized);
        SettingsChanged?.Invoke(normalized);
        return normalized;
    }

    public static VoiceChatSettings Normalize(VoiceChatSettings? settings)
    {
        var language = string.IsNullOrWhiteSpace(settings?.Language)
            ? DefaultLanguage
            : settings.Language.Trim();

        var wakePhrase = WakePhrases.FirstOrDefault(
            phrase => string.Equals(phrase, settings?.WakePhrase?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? DefaultWakePhrase;

        var wakeSensitivity = WakeSensitivities.FirstOrDefault(
            value => string.Equals(value, settings?.WakeSensitivity?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? DefaultWakeSensitivity;

        return new VoiceChatSettings
        {
            Language = language,
            AutoContinueEnabled = settings?.AutoContinueEnabled ?? true,
            WakePhrase = wakePhrase,
            WakeSensitivity = wakeSensitivity,
            AutoListenOnWake = settings?.AutoListenOnWake ?? true,
            PicovoiceAccessKey = settings?.PicovoiceAccessKey?.Trim() ?? string.Empty
        };
    }
}
