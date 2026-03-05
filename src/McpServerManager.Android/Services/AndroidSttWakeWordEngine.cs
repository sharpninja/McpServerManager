using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Media;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using Vosk;

namespace McpServerManager.Android.Services;

/// <summary>
/// Wake-word engine backed by Vosk offline speech recognition and raw AudioRecord capture.
/// No API key required — completely free and open source. Uses a grammar-constrained recognizer
/// limited to the configured wake phrases for fast, low-resource detection.
/// </summary>
public sealed class AndroidVoskWakeWordEngine : IAndroidWakeWordEngine
{
    private const int SampleRate = 16000;
    private const string ModelAssetFolder = "Voice/VoskModel";

    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("AndroidVoskWakeWordEngine");

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _loopTask;
    private AudioRecord? _audioRecord;
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private IDisposable? _crashBoundary;
    private AndroidWakeWordSettings _settings = new();
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event EventHandler<AndroidWakeWordDetectedEventArgs>? WakeWordDetected;

    public Task ConfigureAsync(AndroidWakeWordSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settings = settings ?? new AndroidWakeWordSettings();
        Logger.LogInformation("Configured with wake phrase: {Phrase}", _settings.SelectedWakePhrase);
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
                return;

            var context = Application.Context
                ?? throw new InvalidOperationException("Android application context is unavailable.");

            await EnsureRecordAudioPermissionAsync(cancellationToken);

            var modelPath = await MaterializeModelAsync(context, cancellationToken);

            Logger.LogInformation("Loading Vosk model from {Path}...", modelPath);
            Vosk.Vosk.SetLogLevel(-1); // suppress native Vosk logs
            _model = new Model(modelPath);

            // Grammar-constrained recognizer — only listens for wake phrases + filler
            var phrases = AndroidWakeWordCatalog.SupportedWakePhrases;
            var grammarWords = phrases
                .SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .Append("[unk]")
                .ToArray();
            var grammar = "[\"" + string.Join("\", \"", grammarWords) + "\"]";
            Logger.LogInformation("Vosk grammar: {Grammar}", grammar);

            _recognizer = new VoskRecognizer(_model, SampleRate, grammar);
            _recognizer.SetWords(true);

            var audioRecord = CreateAudioRecord();
            audioRecord.StartRecording();

            if (audioRecord.RecordingState != RecordState.Recording)
            {
                audioRecord.Dispose();
                throw new InvalidOperationException("AudioRecord failed to start recording for Vosk wake word.");
            }

            _audioRecord = audioRecord;
            _runCts = new CancellationTokenSource();
            _crashBoundary = AndroidCrashDiagnostics.BeginBoundary(
                "VoskWakeWordMonitoring",
                $"Phrase={_settings.SelectedWakePhrase}; SampleRate={SampleRate}");
            IsRunning = true;
            _loopTask = Task.Run(() => DetectionLoopAsync(_runCts.Token), CancellationToken.None);

            Logger.LogInformation("Vosk wake-word engine started for phrase '{Phrase}'.", _settings.SelectedWakePhrase);
        }
        catch (Exception ex) when (ex is not System.OperationCanceledException)
        {
            CleanupEngineState();
            Logger.LogError(ex, "Failed to start Vosk wake-word engine.");
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
                return;

            Logger.LogInformation("Stopping Vosk wake-word engine.");
            IsRunning = false;

            try { _runCts?.Cancel(); } catch { }

            var loopTask = _loopTask;
            _loopTask = null;
            if (loopTask != null)
            {
                try { await loopTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch { }
            }

            CleanupEngineState();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _runCts?.Cancel(); } catch { }
        CleanupEngineState();
        _lifecycleLock.Dispose();
    }

    private async Task DetectionLoopAsync(CancellationToken ct)
    {
        const int frameLengthSamples = 4096; // ~256ms at 16kHz
        var buffer = new short[frameLengthSamples];
        var consecutiveErrors = 0;

        while (!ct.IsCancellationRequested && IsRunning)
        {
            var audioRecord = _audioRecord;
            var recognizer = _recognizer;
            if (audioRecord == null || recognizer == null)
                break;

            var offset = 0;
            while (offset < buffer.Length && !ct.IsCancellationRequested)
            {
                int readCount;
                try
                {
                    readCount = audioRecord.Read(buffer, offset, buffer.Length - offset);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Logger.LogWarning(ex, "AudioRecord read failed.");
                    consecutiveErrors++;
                    if (consecutiveErrors >= 8)
                        throw;

                    try { await Task.Delay(20, ct); }
                    catch (System.OperationCanceledException) { return; }
                    continue;
                }

                if (readCount <= 0)
                {
                    consecutiveErrors++;
                    if (consecutiveErrors >= 8)
                        break;

                    try { await Task.Delay(20, ct); }
                    catch (System.OperationCanceledException) { return; }
                    continue;
                }

                consecutiveErrors = 0;
                offset += readCount;
            }

            if (offset < buffer.Length || ct.IsCancellationRequested)
                break;

            // Feed audio to Vosk
            bool isComplete;
            try
            {
                isComplete = recognizer.AcceptWaveform(buffer, buffer.Length);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Vosk AcceptWaveform failed.");
                break;
            }

            if (isComplete)
            {
                var resultJson = recognizer.Result();
                CheckForWakePhrase(resultJson, "final");
            }
            else
            {
                var partialJson = recognizer.PartialResult();
                CheckForWakePhrase(partialJson, "partial");
            }
        }

        Logger.LogInformation("Vosk wake-word detection loop exited.");
    }

    private void CheckForWakePhrase(string json, string resultType)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Vosk returns {"text": "..."} for results and {"partial": "..."} for partial results
            var textProp = resultType == "partial" ? "partial" : "text";
            if (!root.TryGetProperty(textProp, out var textElement))
                return;

            var text = textElement.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var wakePhraseNorm = _settings.SelectedWakePhrase.ToLowerInvariant();
            var textNorm = text.ToLowerInvariant();

            if (textNorm.Contains(wakePhraseNorm, StringComparison.Ordinal))
            {
                Logger.LogInformation("Wake phrase '{Phrase}' detected in {Type}: \"{Text}\"",
                    _settings.SelectedWakePhrase, resultType, text);

                // Reset recognizer to avoid re-detecting same audio
                _recognizer?.Reset();

                WakeWordDetected?.Invoke(this,
                    new AndroidWakeWordDetectedEventArgs(_settings.SelectedWakePhrase, DateTimeOffset.UtcNow));
            }
        }
        catch (JsonException)
        {
            // Ignore malformed JSON from Vosk.
        }
    }

    private void CleanupEngineState()
    {
        try { _audioRecord?.Stop(); } catch { }
        try { _audioRecord?.Dispose(); } catch { }
        _audioRecord = null;

        try { _recognizer?.Dispose(); } catch { }
        _recognizer = null;

        try { _model?.Dispose(); } catch { }
        _model = null;

        _runCts?.Dispose();
        _runCts = null;

        try { _crashBoundary?.Dispose(); } catch { }
        _crashBoundary = null;
    }

    private static AudioRecord CreateAudioRecord()
    {
        const ChannelIn channelIn = ChannelIn.Mono;
        const global::Android.Media.Encoding encoding = global::Android.Media.Encoding.Pcm16bit;

        var minBufferSize = AudioRecord.GetMinBufferSize(SampleRate, channelIn, encoding);
        if (minBufferSize <= 0)
            throw new InvalidOperationException($"AudioRecord returned invalid buffer size ({minBufferSize}).");

        var bufferSize = Math.Max(minBufferSize, SampleRate * 2); // at least 1 second buffer
        var audioRecord = new AudioRecord(AudioSource.Mic, SampleRate, channelIn, encoding, bufferSize);

        if (audioRecord.State != State.Initialized)
        {
            audioRecord.Dispose();
            throw new InvalidOperationException("AudioRecord failed to initialize for Vosk wake word.");
        }

        return audioRecord;
    }

    /// <summary>
    /// Copies the Vosk model from Android assets to the app's files directory (assets can't be read
    /// as a directory path by Vosk — it needs a real filesystem path).
    /// </summary>
    private static async Task<string> MaterializeModelAsync(global::Android.Content.Context context, CancellationToken ct)
    {
        var filesDir = context.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android files directory is unavailable.");

        var modelDir = Path.Combine(filesDir, "vosk-model");

        // Check if model is already materialized
        var markerFile = Path.Combine(modelDir, ".materialized");
        if (File.Exists(markerFile))
        {
            Logger.LogDebug("Vosk model already materialized at {Path}.", modelDir);
            return modelDir;
        }

        Logger.LogInformation("Materializing Vosk model from assets to {Path}...", modelDir);
        if (Directory.Exists(modelDir))
            Directory.Delete(modelDir, true);

        var assetManager = context.Assets
            ?? throw new InvalidOperationException("Android AssetManager is unavailable.");

        await CopyAssetFolderAsync(assetManager, ModelAssetFolder, modelDir, ct);

        // Write marker
        await File.WriteAllTextAsync(markerFile, DateTimeOffset.UtcNow.ToString("O"), ct);
        Logger.LogInformation("Vosk model materialized successfully.");
        return modelDir;
    }

    private static async Task CopyAssetFolderAsync(
        global::Android.Content.Res.AssetManager assets,
        string assetPath,
        string destPath,
        CancellationToken ct)
    {
        Directory.CreateDirectory(destPath);
        var entries = assets.List(assetPath);
        if (entries == null || entries.Length == 0)
            return;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var srcPath = $"{assetPath}/{entry}";
            var dstPath = Path.Combine(destPath, entry);

            var subEntries = assets.List(srcPath);
            if (subEntries != null && subEntries.Length > 0)
            {
                // It's a subdirectory
                await CopyAssetFolderAsync(assets, srcPath, dstPath, ct);
            }
            else
            {
                // It's a file
                using var input = assets.Open(srcPath);
                using var output = File.Create(dstPath);
                await input.CopyToAsync(output, ct);
            }
        }
    }

    private static async Task EnsureRecordAudioPermissionAsync(CancellationToken ct)
    {
        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null)
            return;

        var granted = await AndroidActivityHost.RequestRecordAudioPermissionAsync(activity, ct);
        if (!granted)
            throw new InvalidOperationException("Microphone permission is required for wake-word monitoring.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AndroidVoskWakeWordEngine));
    }
}
