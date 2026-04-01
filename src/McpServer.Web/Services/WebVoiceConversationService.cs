using System.Runtime.CompilerServices;
using McpServer.Client;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Services;

internal sealed class WebVoiceConversationService : IVoiceConversationService
{
    private readonly WebMcpContext _context;

    public WebVoiceConversationService(WebMcpContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ResolveWorkspacePath = () => _context.ActiveWorkspacePath;
    }

    public Func<string?>? ResolveWorkspacePath { get; set; }

    public string? WorkspacePath { get; set; }

    public async Task<McpVoiceSessionCreateResponse> CreateSessionAsync(
        McpVoiceSessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.CreateSessionAsync(
                    new VoiceSessionCreateRequest
                    {
                        Language = request.Language,
                        DeviceId = request.DeviceId,
                        ClientName = request.ClientName,
                    },
                    ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new McpVoiceSessionCreateResponse
        {
            SessionId = response.SessionId,
            Status = response.Status,
            Language = response.Language,
            ModelRequested = response.ModelRequested,
            ModelResolved = response.ModelResolved,
        };
    }

    public async Task<McpVoiceTurnResponse> SubmitTurnAsync(
        string sessionId,
        McpVoiceTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.SubmitTurnAsync(sessionId, Map(request), ct),
                cancellationToken)
            .ConfigureAwait(true);

        return Map(response);
    }

    public async IAsyncEnumerable<McpVoiceTurnStreamEvent> SubmitTurnStreamingAsync(
        string sessionId,
        McpVoiceTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        await foreach (var item in _context.StreamActiveWorkspaceApiClientAsync(
                           (client, ct) => client.Voice.SubmitTurnStreamingAsync(sessionId, Map(request), ct),
                           cancellationToken)
                       .WithCancellation(cancellationToken)
                       .ConfigureAwait(true))
        {
            yield return Map(item);
        }
    }

    public async Task<McpVoiceInterruptResponse> InterruptAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.InterruptAsync(sessionId, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new McpVoiceInterruptResponse
        {
            SessionId = response.SessionId,
            Interrupted = response.Interrupted,
            Status = response.Status,
        };
    }

    public async Task<bool> SendEscapeAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        try
        {
            var response = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Voice.EscapeAsync(sessionId, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return response.Sent;
        }
        catch (McpNotFoundException)
        {
            return false;
        }
    }

    public async Task<McpVoiceSessionStatus> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.GetStatusAsync(sessionId, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return Map(response);
    }

    public async Task<McpVoiceTranscriptResponse> GetTranscriptAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.GetTranscriptAsync(sessionId, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new McpVoiceTranscriptResponse
        {
            SessionId = response.SessionId,
            Items = response.Items.Select(Map).ToList(),
        };
    }

    public async Task<McpVoiceSessionStatus?> FindExistingSessionAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        try
        {
            var response = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Voice.FindSessionByDeviceAsync(deviceId, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return Map(response);
        }
        catch (McpNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ApplyWorkspaceContextOverride();

        await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.DeleteSessionAsync(sessionId, ct),
                cancellationToken)
            .ConfigureAwait(true);
    }

    private void ApplyWorkspaceContextOverride()
    {
        var workspacePath = ResolveWorkspacePath?.Invoke() ?? WorkspacePath;
        if (!string.IsNullOrWhiteSpace(workspacePath))
            _context.TrySetActiveWorkspace(workspacePath);
    }

    private static VoiceTurnRequest Map(McpVoiceTurnRequest request)
        => new()
        {
            UserTranscriptText = request.UserTranscriptText,
            Language = request.Language,
            ClientTimestampUtc = request.ClientTimestampUtc,
        };

    private static McpVoiceTurnResponse Map(VoiceTurnResponse response)
        => new()
        {
            SessionId = response.SessionId,
            TurnId = response.TurnId,
            Status = response.Status,
            AssistantDisplayText = response.AssistantDisplayText,
            AssistantSpeakText = response.AssistantSpeakText,
            ToolCalls = response.ToolCalls?.Select(Map).ToList(),
            Error = response.Error,
            LatencyMs = response.LatencyMs,
            ModelRequested = response.ModelRequested,
            ModelResolved = response.ModelResolved,
        };

    private static McpVoiceSessionStatus Map(VoiceSessionStatus response)
        => new()
        {
            SessionId = response.SessionId,
            Status = response.Status,
            Language = response.Language,
            CreatedUtc = response.CreatedUtc,
            LastUpdatedUtc = response.LastUpdatedUtc,
            IsTurnActive = response.IsTurnActive,
            LastError = response.LastError,
            LastTurnId = response.LastTurnId,
            TurnCounter = response.TurnCounter,
            TranscriptCount = response.TranscriptCount,
        };

    private static McpVoiceTranscriptEntry Map(VoiceTranscriptEntry item)
        => new()
        {
            TimestampUtc = item.TimestampUtc,
            TurnId = item.TurnId,
            Role = item.Role,
            Category = item.Category,
            Text = item.Text,
        };

    private static McpVoiceToolCallRecord Map(VoiceToolCallRecord item)
        => new()
        {
            TurnId = item.TurnId,
            ToolName = item.ToolName,
            Step = item.Step,
            ArgumentsJson = item.ArgumentsJson,
            Status = item.Status,
            IsMutation = item.IsMutation,
            ResultSummary = item.ResultSummary,
            Error = item.Error,
        };

    private static McpVoiceTurnStreamEvent Map(VoiceTurnStreamEvent item)
        => new()
        {
            Type = item.Type,
            Text = item.Text,
            TurnId = item.TurnId,
            Status = item.Status,
            Message = item.Message,
            ToolName = item.ToolName,
            Summary = item.Summary,
            ToolCalls = item.ToolCalls?.Select(Map).ToList(),
            LatencyMs = item.LatencyMs,
        };
}
