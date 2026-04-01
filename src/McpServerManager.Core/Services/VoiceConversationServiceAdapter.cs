using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using CoreModels = McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

/// <summary>
/// Adapts <see cref="McpVoiceConversationService"/> to UI.Core voice conversation abstraction.
/// </summary>
public sealed class VoiceConversationServiceAdapter : IVoiceConversationService
{
    private readonly McpVoiceConversationService _inner;

    /// <summary>
    /// Creates a new adapter instance.
    /// </summary>
    public VoiceConversationServiceAdapter(McpVoiceConversationService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public Func<string?>? ResolveWorkspacePath
    {
        get => _inner.ResolveWorkspacePath;
        set => _inner.ResolveWorkspacePath = value;
    }

    /// <inheritdoc />
    public string? WorkspacePath
    {
        get => _inner.WorkspacePath;
        set => _inner.WorkspacePath = value;
    }

    /// <inheritdoc />
    public async Task<McpVoiceSessionCreateResponse> CreateSessionAsync(
        McpVoiceSessionCreateRequest request,
        CancellationToken cancellationToken = default)
        => Map(await _inner.CreateSessionAsync(Map(request), cancellationToken).ConfigureAwait(true));

    /// <inheritdoc />
    public async Task<McpVoiceTurnResponse> SubmitTurnAsync(
        string sessionId,
        McpVoiceTurnRequest request,
        CancellationToken cancellationToken = default)
        => Map(await _inner.SubmitTurnAsync(sessionId, Map(request), cancellationToken).ConfigureAwait(true));

    /// <inheritdoc />
    public async IAsyncEnumerable<McpVoiceTurnStreamEvent> SubmitTurnStreamingAsync(
        string sessionId,
        McpVoiceTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _inner.SubmitTurnStreamingAsync(sessionId, Map(request), cancellationToken).ConfigureAwait(true))
            yield return Map(evt);
    }

    /// <inheritdoc />
    public async Task<McpVoiceInterruptResponse> InterruptAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => Map(await _inner.InterruptAsync(sessionId, cancellationToken).ConfigureAwait(true));

    /// <inheritdoc />
    public Task<bool> SendEscapeAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => _inner.SendEscapeAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public async Task<McpVoiceSessionStatus> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => Map(await _inner.GetStatusAsync(sessionId, cancellationToken).ConfigureAwait(true));

    /// <inheritdoc />
    public async Task<McpVoiceTranscriptResponse> GetTranscriptAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => Map(await _inner.GetTranscriptAsync(sessionId, cancellationToken).ConfigureAwait(true));

    /// <inheritdoc />
    public async Task<McpVoiceSessionStatus?> FindExistingSessionAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var status = await _inner.FindExistingSessionAsync(deviceId, cancellationToken).ConfigureAwait(true);
        return status is null ? null : Map(status);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => _inner.DeleteSessionAsync(sessionId, cancellationToken);

    private static CoreModels.McpVoiceSessionCreateRequest Map(McpVoiceSessionCreateRequest source)
        => new()
        {
            Language = source.Language,
            DeviceId = source.DeviceId,
            ClientName = source.ClientName
        };

    private static CoreModels.McpVoiceTurnRequest Map(McpVoiceTurnRequest source)
        => new()
        {
            UserTranscriptText = source.UserTranscriptText,
            Language = source.Language,
            ClientTimestampUtc = source.ClientTimestampUtc
        };

    private static McpVoiceSessionCreateResponse Map(CoreModels.McpVoiceSessionCreateResponse source)
        => new()
        {
            SessionId = source.SessionId,
            Status = source.Status,
            Language = source.Language,
            ModelRequested = source.ModelRequested,
            ModelResolved = source.ModelResolved
        };

    private static McpVoiceTurnResponse Map(CoreModels.McpVoiceTurnResponse source)
        => new()
        {
            SessionId = source.SessionId,
            TurnId = source.TurnId,
            Status = source.Status,
            AssistantDisplayText = source.AssistantDisplayText,
            AssistantSpeakText = source.AssistantSpeakText,
            ToolCalls = source.ToolCalls?.Select(Map).ToList(),
            Error = source.Error,
            LatencyMs = source.LatencyMs,
            ModelRequested = source.ModelRequested,
            ModelResolved = source.ModelResolved
        };

    private static McpVoiceInterruptResponse Map(CoreModels.McpVoiceInterruptResponse source)
        => new()
        {
            SessionId = source.SessionId,
            Interrupted = source.Interrupted,
            Status = source.Status
        };

    private static McpVoiceSessionStatus Map(CoreModels.McpVoiceSessionStatus source)
        => new()
        {
            SessionId = source.SessionId,
            Status = source.Status,
            Language = source.Language,
            CreatedUtc = source.CreatedUtc,
            LastUpdatedUtc = source.LastUpdatedUtc,
            IsTurnActive = source.IsTurnActive,
            LastError = source.LastError,
            LastTurnId = source.LastTurnId,
            TurnCounter = source.TurnCounter,
            TranscriptCount = source.TranscriptCount
        };

    private static McpVoiceTranscriptResponse Map(CoreModels.McpVoiceTranscriptResponse source)
        => new()
        {
            SessionId = source.SessionId,
            Items = source.Items.Select(Map).ToList()
        };

    private static McpVoiceTranscriptEntry Map(CoreModels.McpVoiceTranscriptEntry source)
        => new()
        {
            TimestampUtc = source.TimestampUtc,
            TurnId = source.TurnId,
            Role = source.Role,
            Category = source.Category,
            Text = source.Text
        };

    private static McpVoiceToolCallRecord Map(CoreModels.McpVoiceToolCallRecord source)
        => new()
        {
            TurnId = source.TurnId,
            ToolName = source.ToolName,
            Step = source.Step,
            ArgumentsJson = source.ArgumentsJson,
            Status = source.Status,
            IsMutation = source.IsMutation,
            ResultSummary = source.ResultSummary,
            Error = source.Error
        };

    private static McpVoiceTurnStreamEvent Map(CoreModels.McpVoiceTurnStreamEvent source)
        => new()
        {
            Type = source.Type,
            Text = source.Text,
            TurnId = source.TurnId,
            Status = source.Status,
            Message = source.Message,
            ToolName = source.ToolName,
            Summary = source.Summary,
            ToolCalls = source.ToolCalls?.Select(Map).ToList(),
            LatencyMs = source.LatencyMs
        };
}
