using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// UI-state ViewModel for the Android voice conversation tab (manual transcript + MCP voice endpoint integration).
/// </summary>
public partial class VoiceConversationViewModel : ViewModelBase
{
    private readonly ILogger<VoiceConversationViewModel> _logger;
    private readonly IClipboardService _clipboardService;
    private static readonly Regex AnsiEscapePattern = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions TranscriptJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IVoiceConversationService _voiceService;
    private CancellationTokenSource? _activeTurnCts;

    [ObservableProperty] private string _sessionId = string.Empty;
    [ObservableProperty] private string _language = "en-US";
    [ObservableProperty] private string _clientName = "RequestTracker.Android";
    [ObservableProperty] private string _statusText = "Voice ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _transcriptInput = string.Empty;
    [ObservableProperty] private string _assistantDisplayText = string.Empty;
    [ObservableProperty] private string _assistantSpeakText = string.Empty;
    [ObservableProperty] private string _lastTurnId = string.Empty;
    [ObservableProperty] private int _lastLatencyMs;
    [ObservableProperty] private bool _isSessionActive;

    /// <summary>When set, workspace path is resolved from the source of truth at read time.</summary>
    public Func<string?>? ResolveWorkspacePath { get; set; }

    /// <summary>When set, indicates whether workspace state is ready for creating voice sessions.</summary>
    public Func<bool>? ResolveWorkspaceReady { get; set; }

    /// <summary>True when workspace selection/switch state is ready for voice session start.</summary>
    public bool IsWorkspaceReady => ResolveWorkspaceReady?.Invoke() ?? !string.IsNullOrWhiteSpace(WorkspacePath);

    /// <summary>The active workspace root path (e.g. "E:\github\FunWasHad"). Reads from <see cref="ResolveWorkspacePath"/> when set.</summary>
    public string WorkspacePath
    {
        get => ResolveWorkspacePath?.Invoke() ?? _workspacePath;
        set
        {
            _workspacePath = value ?? string.Empty;
            // Only push to service if the service doesn't have its own resolver
            if (_voiceService.ResolveWorkspacePath == null)
                _voiceService.WorkspacePath = _workspacePath;
        }
    }
    private string _workspacePath = string.Empty;

    /// <summary>Transcript items for the active session.</summary>
    public ObservableCollection<McpVoiceTranscriptEntry> TranscriptItems { get; } = [];

    /// <summary>Tool-call records from the most recent turn.</summary>
    public ObservableCollection<McpVoiceToolCallRecord> LastToolCalls { get; } = [];

    /// <summary>Raised when a status message should be displayed in the global status bar.</summary>
    public event Action<string>? GlobalStatusChanged;

    /// <summary>
    /// Creates a voice conversation ViewModel with a pre-configured voice service.
    /// </summary>
    public VoiceConversationViewModel(
        IVoiceConversationService service,
        ILogger<VoiceConversationViewModel>? logger = null,
        IClipboardService? clipboardService = null)
    {
        _voiceService = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? NullLogger<VoiceConversationViewModel>.Instance;
        _clipboardService = clipboardService ?? new NoOpClipboardService();
    }

    /// <summary>
    /// Refreshes for a connection change by clearing the prior remote session state.
    /// </summary>
    public Task RefreshForConnectionChangeAsync()
    {
        _activeTurnCts?.Cancel();
        ClearSessionState();
        StatusText = "Voice session reset after connection change.";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates or resumes a voice session.
    /// </summary>
    protected async Task CreateSessionAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var deviceId = Environment.MachineName;

            // Try to reconnect to an existing session for this device
            GlobalStatusChanged?.Invoke("Looking for existing voice session...");
            StatusText = "Looking for existing voice session...";
            try
            {
                var existing = await _voiceService.FindExistingSessionAsync(deviceId).ConfigureAwait(true);
                if (existing is not null)
                {
                    SessionId = existing.SessionId;
                    Language = string.IsNullOrWhiteSpace(existing.Language) ? Language : existing.Language;
                    IsSessionActive = true;
                    LastTurnId = existing.LastTurnId ?? string.Empty;
                    StatusText = $"Resumed session {SessionId} (turn {existing.TurnCounter})";
                    GlobalStatusChanged?.Invoke(StatusText);
                    _logger.LogInformation("Resumed existing voice session {SessionId} (turns={TurnCounter}, transcripts={TranscriptCount})",
                        SessionId, existing.TurnCounter, existing.TranscriptCount);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Device session lookup failed, falling back to create");
            }

            // No existing session — create a new one
            GlobalStatusChanged?.Invoke("Creating voice session...");
            StatusText = "Creating voice session...";
            var response = await _voiceService.CreateSessionAsync(new McpVoiceSessionCreateRequest
            {
                Language = Language,
                ClientName = string.IsNullOrWhiteSpace(ClientName) ? "RequestTracker.Android" : ClientName.Trim(),
                DeviceId = deviceId
            }).ConfigureAwait(true);

            SessionId = response.SessionId;
            Language = string.IsNullOrWhiteSpace(response.Language) ? Language : response.Language;
            IsSessionActive = true;
            TranscriptItems.Clear();
            LastToolCalls.Clear();
            AssistantDisplayText = string.Empty;
            AssistantSpeakText = string.Empty;
            LastTurnId = string.Empty;
            LastLatencyMs = 0;
            StatusText = $"Voice session created: {SessionId}";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create voice session failed");
            StatusText = $"Create session failed: {ex.Message}";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Submits the current transcript input as a voice turn.
    /// </summary>
    protected async Task SubmitTurnAsync()
    {
        if (IsBusy) return;

        var text = (TranscriptInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText = "Enter transcript text first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SessionId))
            await CreateSessionAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(SessionId))
            return;

        IsBusy = true;
        _activeTurnCts?.Cancel();
        _activeTurnCts = new CancellationTokenSource();
        var ct = _activeTurnCts.Token;

        try
        {
            GlobalStatusChanged?.Invoke("Submitting voice turn...");
            StatusText = "Submitting voice turn...";
            var response = await _voiceService.SubmitTurnAsync(
                SessionId,
                new McpVoiceTurnRequest
                {
                    UserTranscriptText = text,
                    Language = Language,
                    ClientTimestampUtc = DateTimeOffset.UtcNow.ToString("O")
                },
                ct).ConfigureAwait(true);

            LastTurnId = response.TurnId ?? string.Empty;
            LastLatencyMs = response.LatencyMs;
            AssistantDisplayText = response.AssistantDisplayText ?? string.Empty;
            AssistantSpeakText = response.AssistantSpeakText ?? string.Empty;

            LastToolCalls.Clear();
            if (response.ToolCalls != null)
            {
                foreach (var toolCall in response.ToolCalls)
                    LastToolCalls.Add(toolCall);
            }

            await RefreshTranscriptAsyncInternal(ct).ConfigureAwait(true);

            StatusText = string.Equals(response.Status, "completed", StringComparison.OrdinalIgnoreCase)
                ? $"Voice turn completed ({response.LatencyMs} ms)"
                : $"Voice turn {response.Status}: {response.Error ?? "no details"}";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("Voice turn submission canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Submit voice turn failed");
            StatusText = $"Voice turn failed: {ex.Message}";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Submits a turn via SSE streaming. Yields text chunks as they arrive from Copilot.
    /// Updates <see cref="AssistantDisplayText"/> incrementally.
    /// </summary>
    public async IAsyncEnumerable<McpVoiceTurnStreamEvent> SubmitTurnStreamingAsync(
        string text,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        if (string.IsNullOrWhiteSpace(SessionId))
            await CreateSessionAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(SessionId))
            yield break;

        IsBusy = true;
        _activeTurnCts?.Cancel();
        _activeTurnCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_activeTurnCts.Token, cancellationToken);

        AssistantDisplayText = string.Empty;
        AssistantSpeakText = string.Empty;
        StatusText = "Streaming response...";
        GlobalStatusChanged?.Invoke(StatusText);

        var channel = System.Threading.Channels.Channel.CreateUnbounded<McpVoiceTurnStreamEvent>();
        var accumulated = new System.Text.StringBuilder();

        // Producer: reads from service stream, writes to channel
        _ = Task.Run(async () =>
        {
            McpVoiceTurnStreamEvent? lastEvent = null;
            try
            {
                await foreach (var evt in _voiceService.SubmitTurnStreamingAsync(
                    SessionId,
                    new McpVoiceTurnRequest
                    {
                        UserTranscriptText = text,
                        Language = Language,
                        ClientTimestampUtc = DateTimeOffset.UtcNow.ToString("O")
                    },
                    linkedCts.Token))
                {
                    lastEvent = evt;
                    if (evt.Type == "chunk" && evt.Text is not null)
                    {
                        accumulated.Append(AnsiEscapePattern.Replace(evt.Text, ""));
                        AssistantDisplayText = accumulated.ToString();
                    }
                    await channel.Writer.WriteAsync(evt, linkedCts.Token);
                }
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                _logger.LogDebug("Voice turn streaming canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming voice turn failed");
                StatusText = $"Voice turn failed: {ex.Message}";
                GlobalStatusChanged?.Invoke(StatusText);
                await channel.Writer.WriteAsync(
                    new McpVoiceTurnStreamEvent { Type = "error", Message = ex.Message },
                    CancellationToken.None);
            }
            finally
            {
                // Finalize state
                var finalText = accumulated.ToString().Trim();

                if (lastEvent?.Type == "done")
                {
                    LastTurnId = lastEvent.TurnId ?? string.Empty;
                    LastLatencyMs = lastEvent.LatencyMs ?? 0;
                    LastToolCalls.Clear();
                    if (lastEvent.ToolCalls is not null)
                        foreach (var tc in lastEvent.ToolCalls)
                            LastToolCalls.Add(tc);
                    if (string.IsNullOrWhiteSpace(finalText))
                        finalText = await TryLoadLatestAssistantTranscriptTextAsync(lastEvent.TurnId, CancellationToken.None).ConfigureAwait(true);
                    StatusText = $"Voice turn completed ({LastLatencyMs} ms)";
                }
                else if (lastEvent?.Type == "error")
                {
                    StatusText = $"Voice turn error: {lastEvent.Message}";
                }

                AssistantDisplayText = finalText;
                AssistantSpeakText = finalText;
                GlobalStatusChanged?.Invoke(StatusText);
                IsBusy = false;
                linkedCts.Dispose();
                channel.Writer.Complete();
            }
        }, linkedCts.Token);

        // Consumer: yield from channel
        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(true))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Refreshes transcript history from the service.
    /// </summary>
    protected async Task RefreshTranscriptAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await RefreshTranscriptAsyncInternal(CancellationToken.None).ConfigureAwait(true);
            StatusText = $"Loaded {TranscriptItems.Count} transcript item(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh transcript failed");
            StatusText = $"Refresh transcript failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Copies the current complete transcript snapshot to the host clipboard as plain text.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CopyTranscriptAsync(CancellationToken cancellationToken = default)
    {
        var text = await BuildTranscriptTextForExportAsync(cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(text))
        {
            SetExportStatus("No transcript entries available to copy.");
            return;
        }

        await _clipboardService.SetTextAsync(text).ConfigureAwait(true);
        SetExportStatus($"Copied {TranscriptItems.Count} transcript item(s).");
    }

    /// <summary>
    /// Builds the current complete transcript snapshot as plain text, refreshing from the service first when possible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Plain-text transcript.</returns>
    public async Task<string> BuildTranscriptTextForExportAsync(CancellationToken cancellationToken = default)
    {
        await RefreshTranscriptSnapshotForExportAsync(cancellationToken).ConfigureAwait(true);
        return BuildTranscriptText(TranscriptItems);
    }

    /// <summary>
    /// Builds the current complete transcript snapshot as JSON Lines, refreshing from the service first when possible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSONL transcript with one transcript entry per line.</returns>
    public async Task<string> BuildTranscriptJsonLinesForExportAsync(CancellationToken cancellationToken = default)
    {
        await RefreshTranscriptSnapshotForExportAsync(cancellationToken).ConfigureAwait(true);
        return BuildTranscriptJsonLines(SessionId, TranscriptItems);
    }

    /// <summary>
    /// Creates a stable suggested filename for transcript JSONL exports.
    /// </summary>
    /// <returns>Suggested JSONL filename.</returns>
    public string CreateTranscriptJsonlFileName()
    {
        var sessionPart = string.IsNullOrWhiteSpace(SessionId)
            ? "voice-session"
            : SanitizeFileNamePart(SessionId);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
        return $"{sessionPart}-transcript-{timestamp}.jsonl";
    }

    /// <summary>
    /// Builds a readable plain-text transcript from voice transcript entries.
    /// </summary>
    /// <param name="items">Transcript entries.</param>
    /// <returns>Plain-text transcript.</returns>
    public static string BuildTranscriptText(IEnumerable<McpVoiceTranscriptEntry> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder();
        foreach (var item in OrderTranscriptItems(items))
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append('[')
                .Append(item.TimestampUtc)
                .Append("] ")
                .Append(NormalizeExportField(item.Role));

            if (!string.IsNullOrWhiteSpace(item.Category))
                builder.Append('/').Append(NormalizeExportField(item.Category));

            if (!string.IsNullOrWhiteSpace(item.TurnId))
                builder.Append(" (").Append(item.TurnId).Append(')');

            builder.AppendLine();
            builder.AppendLine(item.Text ?? string.Empty);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds JSON Lines from voice transcript entries.
    /// </summary>
    /// <param name="sessionId">Voice session id.</param>
    /// <param name="items">Transcript entries.</param>
    /// <returns>JSONL transcript with one entry per line.</returns>
    public static string BuildTranscriptJsonLines(string? sessionId, IEnumerable<McpVoiceTranscriptEntry> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder();
        foreach (var item in OrderTranscriptItems(items))
        {
            var exportLine = new VoiceTranscriptExportLine(
                sessionId,
                item.TimestampUtc,
                item.TurnId,
                item.Role,
                item.Category,
                item.Text);
            builder.AppendLine(JsonSerializer.Serialize(exportLine, TranscriptJsonOptions));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Refreshes session status from the service.
    /// </summary>
    protected async Task RefreshStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            StatusText = "No active session.";
            return;
        }

        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var status = await _voiceService.GetStatusAsync(SessionId).ConfigureAwait(true);
            IsSessionActive = true;
            LastTurnId = status.LastTurnId ?? LastTurnId;
            StatusText = $"{status.Status} (turn active: {status.IsTurnActive})";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh voice status failed");
            StatusText = $"Refresh status failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sends an interrupt request for the current turn.
    /// </summary>
    protected async Task InterruptAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            StatusText = "No active session.";
            return;
        }

        try
        {
            _activeTurnCts?.Cancel();
            var result = await _voiceService.InterruptAsync(SessionId).ConfigureAwait(true);
            StatusText = result.Interrupted ? "Interrupt sent." : "No active turn to interrupt.";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interrupt voice turn failed");
            StatusText = $"Interrupt failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends ESC to the underlying interactive session when available.
    /// </summary>
    protected async Task SendEscapeAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            StatusText = "No active session.";
            return;
        }

        try
        {
            var sent = await _voiceService.SendEscapeAsync(SessionId).ConfigureAwait(true);
            StatusText = sent ? "ESC sent to Copilot." : "No active interactive session.";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send escape failed");
            StatusText = $"Send ESC failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Ends and deletes the active session.
    /// </summary>
    protected async Task EndSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            ClearSessionState();
            return;
        }

        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _activeTurnCts?.Cancel();
            await _voiceService.DeleteSessionAsync(SessionId).ConfigureAwait(true);
            ClearSessionState();
            StatusText = "Voice session ended.";
            GlobalStatusChanged?.Invoke(StatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "End voice session failed");
            StatusText = $"End session failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clears transcript input text.
    /// </summary>
    protected void ClearTurnInput()
    {
        TranscriptInput = string.Empty;
    }

    private async Task RefreshTranscriptAsyncInternal(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SessionId))
            return;

        var response = await _voiceService.GetTranscriptAsync(SessionId, cancellationToken).ConfigureAwait(true);
        TranscriptItems.Clear();
        foreach (var item in response.Items.OrderBy(i => i.TimestampUtc, StringComparer.Ordinal))
            TranscriptItems.Add(item);
    }

    private async Task RefreshTranscriptSnapshotForExportAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            if (TranscriptItems.Count == 0)
                SetExportStatus("No active voice session.");
            return;
        }

        await RefreshTranscriptAsyncInternal(cancellationToken).ConfigureAwait(true);
        SetExportStatus(TranscriptItems.Count == 0
            ? "No transcript entries available to export."
            : $"Prepared {TranscriptItems.Count} transcript item(s) for export.");
    }

    private async Task<string> TryLoadLatestAssistantTranscriptTextAsync(string? turnId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SessionId))
            return string.Empty;

        try
        {
            await RefreshTranscriptAsyncInternal(cancellationToken).ConfigureAwait(true);
            var latest = TranscriptItems
                .Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(turnId) || string.Equals(item.TurnId, turnId, StringComparison.Ordinal))
                .OrderBy(item => item.TimestampUtc, StringComparer.Ordinal)
                .LastOrDefault();

            return latest?.Text?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to load assistant transcript fallback for voice turn {TurnId}", turnId);
            return string.Empty;
        }
    }

    private void SetExportStatus(string message)
    {
        StatusText = message;
        GlobalStatusChanged?.Invoke(message);
    }

    private static IEnumerable<McpVoiceTranscriptEntry> OrderTranscriptItems(IEnumerable<McpVoiceTranscriptEntry> items)
        => items.OrderBy(i => i.TimestampUtc, StringComparer.Ordinal)
            .ThenBy(i => i.TurnId, StringComparer.Ordinal);

    private static string NormalizeExportField(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static string SanitizeFileNamePart(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
        }

        return builder.ToString().Trim('-') is { Length: > 0 } sanitized ? sanitized : "voice-session";
    }

    private void ClearSessionState()
    {
        SessionId = string.Empty;
        IsSessionActive = false;
        TranscriptItems.Clear();
        LastToolCalls.Clear();
        AssistantDisplayText = string.Empty;
        AssistantSpeakText = string.Empty;
        LastTurnId = string.Empty;
        LastLatencyMs = 0;
    }

    private sealed record VoiceTranscriptExportLine(
        string? SessionId,
        string TimestampUtc,
        string? TurnId,
        string Role,
        string Category,
        string Text);
}
