using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using Pv;

namespace McpServerManager.Android.Services;

/// <summary>
/// Porcupine-backed wake-word engine for Android. Requires a Picovoice access key and custom .ppn keyword assets.
/// </summary>
public sealed class AndroidPorcupineWakeWordEngine : IAndroidWakeWordEngine
{
    private const string VoicePreferencesName = "McpServerManager.Voice";
    private const string PicovoiceAccessKeyPreferenceKey = "PicovoiceAccessKey";
    private const string PicovoiceAccessKeyEnvVar = "PICOVOICE_ACCESS_KEY";
    private const string PicovoiceAccessKeyAltEnvVar = "PORCUPINE_ACCESS_KEY";
    private const string PicovoiceAccessKeyMetadataKey = "PICOVOICE_ACCESS_KEY";
    private const string PicovoiceAccessKeyAltMetadataKey = "PicovoiceAccessKey";
    private const string KeywordAssetFolder = "Voice/WakeWords";
    private const string OptionalModelAssetPath = "Voice/Porcupine/porcupine_params.pv";

    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("AndroidPorcupineWakeWordEngine");

    private static readonly IReadOnlyDictionary<string, string> PhraseToKeywordAssetMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hey Tracker"] = "hey_tracker_android.ppn",
            ["Okay Tracker"] = "okay_tracker_android.ppn",
            ["Hello Tracker"] = "hello_tracker_android.ppn"
        };

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _loopTask;
    private Porcupine? _porcupine;
    private AudioRecord? _audioRecord;
    private IDisposable? _crashBoundary;
    private AndroidWakeWordSettings _settings = new();
    private string[] _activePhrases = [];
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;

    public async Task ConfigureAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeSettings(settings);
        var restartRequired = false;

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            restartRequired = IsRunning &&
                             (!string.Equals(_settings.SelectedWakePhrase, normalized.SelectedWakePhrase, StringComparison.Ordinal) ||
                              !string.Equals(_settings.Sensitivity, normalized.Sensitivity, StringComparison.OrdinalIgnoreCase));
            _settings = normalized;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (restartRequired)
        {
            await StopAsync(cancellationToken);
            await StartAsync(cancellationToken);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            if (IsRunning)
                return;

            var context = Application.Context ?? throw new InvalidOperationException("Android application context is unavailable.");
            await EnsureRecordAudioPermissionAsync(cancellationToken);

            var settings = NormalizeSettings(_settings);
            var accessKey = ResolvePicovoiceAccessKey(context);
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                throw new InvalidOperationException(
                    "Picovoice AccessKey is not configured. Set Android app metadata 'PICOVOICE_ACCESS_KEY', " +
                    "environment variable 'PICOVOICE_ACCESS_KEY', or SharedPreferences key 'PicovoiceAccessKey'.");
            }

            var keywordPhrase = settings.SelectedWakePhrase;
            var keywordAssetFileName = GetKeywordAssetFileName(keywordPhrase);
            var keywordAssetPath = $"{KeywordAssetFolder}/{keywordAssetFileName}";
            var keywordFilePath = await MaterializeRequiredAssetAsync(context, keywordAssetPath, cancellationToken);
            var modelFilePath = await MaterializeOptionalAssetAsync(context, OptionalModelAssetPath, cancellationToken);

            var sensitivities = new[] { MapSensitivity(settings.Sensitivity) };
            var porcupine = CreatePorcupine(accessKey.Trim(), keywordFilePath, modelFilePath, sensitivities);
            var audioRecord = CreateAudioRecord(porcupine.SampleRate, porcupine.FrameLength);

            try
            {
                audioRecord.StartRecording();
            }
            catch
            {
                audioRecord.Dispose();
                porcupine.Dispose();
                throw;
            }

            if (audioRecord.RecordingState != RecordState.Recording)
            {
                audioRecord.Dispose();
                porcupine.Dispose();
                throw new InvalidOperationException("Android microphone recording did not enter Recording state.");
            }

            _porcupine = porcupine;
            _audioRecord = audioRecord;
            _activePhrases = [keywordPhrase];
            _runCts = new CancellationTokenSource();
            _crashBoundary = AndroidCrashDiagnostics.BeginBoundary(
                "PorcupineWakeWordMonitoring",
                $"Phrase={keywordPhrase}; Sensitivity={settings.Sensitivity}");
            _loopTask = Task.Run(() => DetectionLoopAsync(porcupine, audioRecord, _activePhrases, _runCts.Token), CancellationToken.None);
            IsRunning = true;

            Logger.LogInformation(
                "Started Porcupine wake-word engine for phrase '{Phrase}' using keyword asset '{KeywordAssetPath}'.",
                keywordPhrase,
                keywordAssetPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CleanupEngineState();
            throw WrapStartupException(ex);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task? loopTask;
        CancellationTokenSource? runCts;
        AudioRecord? audioRecord;
        Porcupine? porcupine;

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
                return;

            if (!IsRunning && _audioRecord == null && _porcupine == null && _loopTask == null)
                return;

            IsRunning = false;
            loopTask = _loopTask;
            runCts = _runCts;
            audioRecord = _audioRecord;
            porcupine = _porcupine;

            _loopTask = null;
            _runCts = null;
            _audioRecord = null;
            _porcupine = null;
            _activePhrases = [];
        }
        finally
        {
            _lifecycleLock.Release();
        }

        try
        {
            runCts?.Cancel();
        }
        catch
        {
            // Best-effort shutdown.
        }

        try
        {
            audioRecord?.Stop();
        }
        catch
        {
            // Best-effort shutdown.
        }

        if (loopTask != null)
        {
            try
            {
                await loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Caller canceled stop; continue cleanup.
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Wake-word detection loop exited with an exception during shutdown.");
            }
        }

        try
        {
            audioRecord?.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }

        try
        {
            porcupine?.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }

        runCts?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort shutdown.
        }

        _disposed = true;
        _lifecycleLock.Dispose();
    }

    private async Task DetectionLoopAsync(Porcupine porcupine, AudioRecord audioRecord, IReadOnlyList<string> phrases, CancellationToken cancellationToken)
    {
        var frame = new short[porcupine.FrameLength];
        var consecutiveReadErrors = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var offset = 0;
            while (offset < frame.Length && !cancellationToken.IsCancellationRequested)
            {
                int readCount;
                try
                {
                    readCount = audioRecord.Read(frame, offset, frame.Length - offset);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException("Android microphone read failed during wake-word monitoring.", ex);
                }

                if (readCount <= 0)
                {
                    consecutiveReadErrors++;
                    if (consecutiveReadErrors >= 8)
                    {
                        throw new InvalidOperationException(
                            $"Android microphone read repeatedly failed during wake-word monitoring (last read={readCount}).");
                    }

                    try
                    {
                        await Task.Delay(20, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                consecutiveReadErrors = 0;
                offset += readCount;
            }

            if (offset < frame.Length || cancellationToken.IsCancellationRequested)
                break;

            int keywordIndex;
            try
            {
                keywordIndex = porcupine.Process(frame);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("Porcupine wake-word processing failed.", ex);
            }

            if (keywordIndex < 0)
                continue;

            var phrase = keywordIndex < phrases.Count ? phrases[keywordIndex] : _settings.SelectedWakePhrase;
            WakeWordDetected?.Invoke(this, new AndroidWakeWordDetectedEventArgs(phrase, DateTimeOffset.UtcNow));
        }
    }

    private static Porcupine CreatePorcupine(
        string accessKey,
        string keywordFilePath,
        string? modelFilePath,
        IReadOnlyList<float> sensitivities)
    {
        try
        {
            return Porcupine.FromKeywordPaths(
                accessKey,
                [keywordFilePath],
                modelPath: string.IsNullOrWhiteSpace(modelFilePath) ? null : modelFilePath,
                device: null,
                sensitivities: sensitivities);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "Porcupine native library could not be loaded on Android. " +
                "The current Porcupine NuGet package does not ship Android native binaries; add Android libpv_porcupine.so assets for your ABIs.",
                ex);
        }
        catch (PorcupineException ex)
        {
            throw new InvalidOperationException($"Porcupine initialization failed: {ex.Message}", ex);
        }
    }

    private static AudioRecord CreateAudioRecord(int sampleRate, int frameLength)
    {
        const ChannelIn channelIn = ChannelIn.Mono;
        const Encoding encoding = Encoding.Pcm16bit;

        var minBufferSize = AudioRecord.GetMinBufferSize(sampleRate, channelIn, encoding);
        if (minBufferSize <= 0)
        {
            throw new InvalidOperationException(
                $"Android AudioRecord returned invalid minimum buffer size ({minBufferSize}) for Porcupine sample rate {sampleRate}.");
        }

        var frameBytes = frameLength * sizeof(short);
        var bufferSize = Math.Max(minBufferSize, frameBytes * 8);
        var audioRecord = new AudioRecord(AudioSource.Mic, sampleRate, channelIn, encoding, bufferSize);

        if (audioRecord.State != State.Initialized)
        {
            audioRecord.Dispose();
            throw new InvalidOperationException("Android AudioRecord failed to initialize for Porcupine wake-word capture.");
        }

        return audioRecord;
    }

    private static async Task EnsureRecordAudioPermissionAsync(CancellationToken cancellationToken)
    {
        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null)
            return;

        var granted = await AndroidActivityHost.RequestRecordAudioPermissionAsync(activity, cancellationToken);
        if (!granted)
            throw new InvalidOperationException("Microphone permission is required for wake-word monitoring.");
    }

    private static AndroidWakeWordSettings NormalizeSettings(AndroidWakeWordSettings? settings)
    {
        if (settings == null)
            return new AndroidWakeWordSettings();

        var phrase = string.IsNullOrWhiteSpace(settings.SelectedWakePhrase)
            ? AndroidWakeWordCatalog.DefaultWakePhrase
            : settings.SelectedWakePhrase.Trim();

        if (!AndroidWakeWordCatalog.SupportedWakePhrases.Any(p => string.Equals(p, phrase, StringComparison.OrdinalIgnoreCase)))
            phrase = AndroidWakeWordCatalog.DefaultWakePhrase;

        var sensitivity = string.IsNullOrWhiteSpace(settings.Sensitivity) ? "medium" : settings.Sensitivity.Trim().ToLowerInvariant();
        if (sensitivity is not ("low" or "medium" or "high"))
            sensitivity = "medium";

        return new AndroidWakeWordSettings
        {
            SelectedWakePhrase = phrase,
            Sensitivity = sensitivity
        };
    }

    private static float MapSensitivity(string? sensitivity) =>
        sensitivity?.Trim().ToLowerInvariant() switch
        {
            "low" => 0.35f,
            "high" => 0.75f,
            _ => 0.55f
        };

    private static string GetKeywordAssetFileName(string phrase)
    {
        if (PhraseToKeywordAssetMap.TryGetValue(phrase, out var fileName))
            return fileName;

        throw new InvalidOperationException(
            $"No Porcupine keyword asset mapping is configured for wake phrase '{phrase}'. " +
            $"Supported phrases: {string.Join(", ", AndroidWakeWordCatalog.SupportedWakePhrases)}.");
    }

    private static string? ResolvePicovoiceAccessKey(Context context)
    {
        var envKey = Environment.GetEnvironmentVariable(PicovoiceAccessKeyEnvVar);
        if (!string.IsNullOrWhiteSpace(envKey))
            return envKey.Trim();

        var altEnvKey = Environment.GetEnvironmentVariable(PicovoiceAccessKeyAltEnvVar);
        if (!string.IsNullOrWhiteSpace(altEnvKey))
            return altEnvKey.Trim();

        try
        {
            var prefs = context.GetSharedPreferences(VoicePreferencesName, FileCreationMode.Private);
            var prefKey = prefs?.GetString(PicovoiceAccessKeyPreferenceKey, null)?.Trim();
            if (!string.IsNullOrWhiteSpace(prefKey))
                return prefKey;
        }
        catch
        {
            // Ignore and continue to other sources.
        }

        try
        {
            var packageName = context.PackageName;
            if (string.IsNullOrWhiteSpace(packageName))
                return null;

            var appInfo = context.PackageManager?.GetApplicationInfo(packageName, PackageInfoFlags.MetaData);
            var meta = appInfo?.MetaData;
            if (meta == null)
                return null;

            var metadataKey = meta.GetString(PicovoiceAccessKeyMetadataKey);
            if (!string.IsNullOrWhiteSpace(metadataKey))
                return metadataKey.Trim();

            var altMetadataKey = meta.GetString(PicovoiceAccessKeyAltMetadataKey);
            if (!string.IsNullOrWhiteSpace(altMetadataKey))
                return altMetadataKey.Trim();
        }
        catch
        {
            // Ignore and return null.
        }

        return null;
    }

    private static async Task<string> MaterializeRequiredAssetAsync(Context context, string assetRelativePath, CancellationToken cancellationToken)
    {
        var path = await MaterializeAssetInternalAsync(context, assetRelativePath, required: true, cancellationToken);
        return path!;
    }

    private static Task<string?> MaterializeOptionalAssetAsync(Context context, string assetRelativePath, CancellationToken cancellationToken) =>
        MaterializeAssetInternalAsync(context, assetRelativePath, required: false, cancellationToken);

    private static async Task<string?> MaterializeAssetInternalAsync(
        Context context,
        string assetRelativePath,
        bool required,
        CancellationToken cancellationToken)
    {
        if (context.Assets == null)
        {
            if (required)
                throw new InvalidOperationException("Android asset manager is unavailable.");

            return null;
        }

        var fileName = Path.GetFileName(assetRelativePath);
        var outputDirectory = Path.Combine(
            context.CacheDir?.AbsolutePath ?? context.FilesDir?.AbsolutePath ?? Path.GetTempPath(),
            "voice",
            "porcupine",
            "assets");
        Directory.CreateDirectory(outputDirectory);
        var destinationPath = Path.Combine(outputDirectory, fileName);

        try
        {
            await using var destination = File.Create(destinationPath);
            await using var source = context.Assets.Open(assetRelativePath);
            await source.CopyToAsync(destination, 81920, cancellationToken);
            return destinationPath;
        }
        catch (Exception ex) when (!required)
        {
            Logger.LogDebug(ex, "Optional Porcupine asset '{AssetPath}' is not available.", assetRelativePath);
            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required Porcupine asset '{assetRelativePath}' was not found. " +
                "Add the custom keyword .ppn file under src/McpServerManager.Android/Assets/Voice/WakeWords/.",
                ex);
        }
    }

    private Exception WrapStartupException(Exception ex)
    {
        if (ex is OperationCanceledException)
            return ex;

        if (ex is InvalidOperationException)
            return ex;

        Logger.LogWarning(ex, "Failed to start Porcupine wake-word engine.");
        return new InvalidOperationException($"Failed to start Porcupine wake-word engine: {ex.Message}", ex);
    }

    private void CleanupEngineState()
    {
        IsRunning = false;
        _activePhrases = [];

        try
        {
            _runCts?.Cancel();
        }
        catch
        {
            // Best effort only.
        }

        try
        {
            _audioRecord?.Stop();
        }
        catch
        {
            // Best effort only.
        }

        try
        {
            _audioRecord?.Dispose();
        }
        catch
        {
            // Best effort only.
        }

        try
        {
            _porcupine?.Dispose();
        }
        catch
        {
            // Best effort only.
        }

        try
        {
            _runCts?.Dispose();
        }
        catch
        {
            // Best effort only.
        }

        _loopTask = null;
        _runCts = null;
        _audioRecord = null;
        _porcupine = null;
        try
        {
            _crashBoundary?.Dispose();
        }
        catch
        {
            // Best effort only.
        }

        _crashBoundary = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidPorcupineWakeWordEngine));
    }
}
