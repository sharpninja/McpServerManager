using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// UI-state ViewModel for the Android voice conversation tab (manual transcript + MCP voice endpoint integration).
/// </summary>
public partial class VoiceConversationViewModel : ViewModelBase
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("VoiceConversationViewModel");

    private McpVoiceConversationService _voiceService;
    private CancellationTokenSource? _activeTurnCts;
    private string _mcpBaseUrl;
    private string? _mcpApiKey;

    [ObservableProperty] private string _sessionId = string.Empty;
    [ObservableProperty] private string _language = "en-US";
    [ObservableProperty] private string _statusText = "Voice ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _transcriptInput = string.Empty;
    [ObservableProperty] private string _assistantDisplayText = string.Empty;
    [ObservableProperty] private string _assistantSpeakText = string.Empty;
    [ObservableProperty] private string _lastTurnId = string.Empty;
    [ObservableProperty] private int _lastLatencyMs;
    [ObservableProperty] private bool _isSessionActive;

    public ObservableCollection<McpVoiceTranscriptEntry> TranscriptItems { get; } = [];
    public ObservableCollection<McpVoiceToolCallRecord> LastToolCalls { get; } = [];

    /// <summary>Raised when a status message should be displayed in the global status bar.</summary>
    public event Action<string>? GlobalStatusChanged;

    /// <summary>
    /// Creates a voice conversation ViewModel for an MCP endpoint.
    /// </summary>
    public VoiceConversationViewModel(string mcpBaseUrl, string? mcpApiKey = null)
    {
        _mcpBaseUrl = mcpBaseUrl;
        _mcpApiKey = mcpApiKey;
        _voiceService = new McpVoiceConversationService(mcpBaseUrl, mcpApiKey);
    }

    /// <summary>
    /// Design-time/parameterless constructor.
    /// </summary>
    public VoiceConversationViewModel() : this(AppSettings.ResolveMcpBaseUrl())
    {
    }

    /// <summary>
    /// Updates the MCP endpoint used by the voice client and clears the current session state.
    /// </summary>
    public void SetMcpBaseUrl(string mcpBaseUrl, string? mcpApiKey = null)
    {
        _mcpBaseUrl = mcpBaseUrl;
        _mcpApiKey = mcpApiKey;
        _voiceService = new McpVoiceConversationService(mcpBaseUrl, mcpApiKey);
        _activeTurnCts?.Cancel();
        ClearSessionState();
        StatusText = "Voice endpoint updated. Create a new voice session.";
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

    [RelayCommand]
    private async Task CreateSessionAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            GlobalStatusChanged?.Invoke("Creating voice session...");
            StatusText = "Creating voice session...";
            var response = await _voiceService.CreateSessionAsync(new McpVoiceSessionCreateRequest
            {
                Language = Language,
                ClientName = "RequestTracker.Android",
                DeviceId = Environment.MachineName
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

    [RelayCommand]
    private async Task SubmitTurnAsync()
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
        catch (OperationCanceledException)
        {
            StatusText = "Voice turn canceled.";
            GlobalStatusChanged?.Invoke(StatusText);
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

    [RelayCommand]
    private async Task RefreshTranscriptAsync()
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

    [RelayCommand]
    private async Task RefreshStatusAsync()
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

    [RelayCommand]
    private async Task InterruptAsync()
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

    [RelayCommand]
    private async Task EndSessionAsync()
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

    [RelayCommand]
    private void ClearTurnInput()
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
}
