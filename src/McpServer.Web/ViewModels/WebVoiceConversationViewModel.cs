using System;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace McpServer.Web.ViewModels;

public sealed class WebVoiceConversationViewModel : VoiceConversationViewModel
{
    private const string SessionReadyStatusText = "Agent chat session ready";
    private const string SessionReadyBubbleMessage = "Agent chat session ready. Type a message to begin the conversation.";

    public WebVoiceConversationViewModel(
        IVoiceConversationService service,
        WorkspaceContextViewModel workspaceContext,
        ILogger<VoiceConversationViewModel>? logger = null)
        : base(service, logger)
    {
        ClientName = "RequestTracker.Web";
        StatusText = SessionReadyStatusText;
        ResolveWorkspacePath = () => workspaceContext.ActiveWorkspacePath;
        ResolveWorkspaceReady = () => !string.IsNullOrWhiteSpace(workspaceContext.ActiveWorkspacePath);
    }

    public bool ShouldShowSessionReadyBubble => IsSessionActive && TranscriptItems.Count == 0;

    public string SessionReadyBubbleText => SessionReadyBubbleMessage;

    public async Task StartOrResumeSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CreateSessionAsync().ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        if (IsSessionActive)
        {
            await RefreshTranscriptAsync().ConfigureAwait(true);
            this.NormalizeReadyStatusText();
        }
    }

    public Task RefreshSessionAsync() => RefreshStatusAsync();

    public async Task RefreshChatAsync()
    {
        await RefreshTranscriptAsync().ConfigureAwait(true);
        this.NormalizeReadyStatusText();
    }

    public Task InterruptTurnAsync() => InterruptAsync();

    public Task SendEscapeToAgentAsync() => SendEscapeAsync();

    public Task EndAgentChatAsync() => EndSessionAsync();

    public void ClearDraft() => ClearTurnInput();

    private void NormalizeReadyStatusText()
    {
        if (!ShouldShowSessionReadyBubble)
            return;

        if (string.IsNullOrWhiteSpace(StatusText)
            || StatusText.StartsWith("Loaded ", StringComparison.Ordinal))
        {
            StatusText = SessionReadyStatusText;
        }
    }
}
