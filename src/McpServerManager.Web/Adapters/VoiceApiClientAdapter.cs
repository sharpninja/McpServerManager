using McpServer.Client;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Web.Adapters;

internal sealed class VoiceApiClientAdapter : IVoiceApiClient
{
    private readonly WebMcpContext _context;
    private readonly ILogger<VoiceApiClientAdapter> _logger;

    public VoiceApiClientAdapter(WebMcpContext context, ILogger<VoiceApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<VoiceApiClientAdapter>.Instance;
    }

    public async Task<VoiceSessionInfo> CreateSessionAsync(CreateVoiceSessionCommand command, CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.CreateSessionAsync(
                    new VoiceSessionCreateRequest
                    {
                        Language = command.Language,
                        DeviceId = command.DeviceId,
                        ClientName = command.ClientName,
                        WorkspacePath = command.WorkspacePath,
                        AgentName = command.AgentName,
                        AgentPath = command.AgentPath,
                        AgentModel = command.AgentModel,
                        AgentSeed = command.AgentSeed,
                        AgentPrompt = command.AgentPrompt,
                        AgentParameters = command.AgentParameters,
                        OneShotSession = command.OneShotSession
                    },
                    ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new VoiceSessionInfo(
            response.SessionId,
            response.Status,
            response.Language,
            response.ModelRequested,
            response.ModelResolved);
    }

    public async Task<VoiceTurnInfo> SubmitTurnAsync(SubmitVoiceTurnCommand command, CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.SubmitTurnAsync(
                    command.SessionId,
                    new VoiceTurnRequest
                    {
                        UserTranscriptText = command.UserTranscriptText,
                        Language = command.Language,
                        ClientTimestampUtc = command.ClientTimestampUtc
                    },
                    ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new VoiceTurnInfo(
            response.SessionId,
            response.TurnId,
            response.Status,
            response.AssistantDisplayText,
            response.AssistantSpeakText,
            response.ToolCalls?.Select(MapToolCall).ToList() ?? [],
            response.Error,
            response.LatencyMs,
            response.ModelRequested,
            response.ModelResolved);
    }

    public async Task<VoiceInterruptInfo> InterruptAsync(InterruptVoiceCommand command, CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Voice.InterruptAsync(command.SessionId, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new VoiceInterruptInfo(response.SessionId, response.Interrupted, response.Status);
    }

    public async Task<VoiceSessionStatusInfo?> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Voice.GetStatusAsync(sessionId, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return new VoiceSessionStatusInfo(
                response.SessionId,
                response.Status,
                response.Language,
                response.CreatedUtc,
                response.LastUpdatedUtc,
                response.IsTurnActive,
                response.LastError,
                response.LastTurnId,
                response.TurnCounter,
                response.TranscriptCount);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<VoiceTranscriptInfo?> GetTranscriptAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Voice.GetTranscriptAsync(sessionId, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return new VoiceTranscriptInfo(
                response.SessionId,
                response.Items.Select(item => new VoiceTranscriptEntryInfo(
                    item.TimestampUtc,
                    item.TurnId,
                    item.Role,
                    item.Category,
                    item.Text)).ToList());
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _context.UseActiveWorkspaceApiClientAsync(
            (client, ct) => client.Voice.DeleteSessionAsync(sessionId, ct),
            cancellationToken);

    private static VoiceToolCallInfo MapToolCall(VoiceToolCallRecord item)
        => new(
            item.TurnId,
            item.ToolName,
            item.Step,
            item.ArgumentsJson,
            item.Status,
            item.IsMutation,
            item.ResultSummary,
            item.Error);
}
