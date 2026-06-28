using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using ChatPreparedPromptMessageResult = McpServerManager.UI.Core.Messages.ChatPreparedPromptResult;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>Thin handler delegating LoadChatPrompts to the chat service.</summary>
internal sealed class LoadChatPromptsQueryHandler : IQueryHandler<LoadChatPromptsQuery, IReadOnlyList<PromptTemplate>>
{
    private readonly IChatWindowService _chatService;
    private readonly ILogger<LoadChatPromptsQueryHandler> _logger;

    public LoadChatPromptsQueryHandler(IChatWindowService chatService, ILogger<LoadChatPromptsQueryHandler> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PromptTemplate>>> HandleAsync(LoadChatPromptsQuery query, CallContext context)
    {
        _logger.LogDebug("Handling LoadChatPromptsQuery");
        var list = await _chatService.LoadPromptsAsync(context.CancellationToken).ConfigureAwait(true);
        return Result<IReadOnlyList<PromptTemplate>>.Success(list);
    }
}

/// <summary>Thin handler for loading models.</summary>
internal sealed class LoadChatModelsQueryHandler : IQueryHandler<LoadChatModelsQuery, ChatLoadModelsResult>
{
    private readonly IChatWindowService _chatService;
    private readonly ILogger<LoadChatModelsQueryHandler> _logger;

    public LoadChatModelsQueryHandler(IChatWindowService chatService, ILogger<LoadChatModelsQueryHandler> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<Result<ChatLoadModelsResult>> HandleAsync(LoadChatModelsQuery query, CallContext context)
    {
        var res = await _chatService.LoadModelsAsync(query.PreferredModel, context.CancellationToken).ConfigureAwait(true);
        return Result<ChatLoadModelsResult>.Success(res);
    }
}

/// <summary>Thin handler for populating prompt text.</summary>
internal sealed class PopulateChatPromptQueryHandler : IQueryHandler<PopulateChatPromptQuery, string>
{
    private readonly IChatWindowService _chatService;

    public PopulateChatPromptQueryHandler(IChatWindowService chatService) => _chatService = chatService;

    public async Task<Result<string>> HandleAsync(PopulateChatPromptQuery query, CallContext context)
    {
        var text = await _chatService.PopulatePromptAsync(query.Prompt, context.CancellationToken).ConfigureAwait(true);
        return Result<string>.Success(text ?? string.Empty);
    }
}

/// <summary>Thin handler for submit prompt decision.</summary>
internal sealed class SubmitChatPromptQueryHandler : IQueryHandler<SubmitChatPromptQuery, ChatPreparedPromptMessageResult>
{
    private readonly IChatWindowService _chatService;

    public SubmitChatPromptQueryHandler(IChatWindowService chatService) => _chatService = chatService;

    public async Task<Result<ChatPreparedPromptMessageResult>> HandleAsync(SubmitChatPromptQuery query, CallContext context)
    {
        var res = await _chatService.SubmitPromptAsync(query.Prompt, context.CancellationToken).ConfigureAwait(true);
        return Result<ChatPreparedPromptMessageResult>.Success(new ChatPreparedPromptMessageResult(res.ShouldSend, res.PromptText));
    }
}

/// <summary>Thin handler for sending a chat message (delegates to service; progress handled by caller if needed).</summary>
internal sealed class SendChatMessageCommandHandler : ICommandHandler<SendChatMessageCommand, ChatSendMessageResult>
{
    private readonly IChatWindowService _chatService;

    public SendChatMessageCommandHandler(IChatWindowService chatService) => _chatService = chatService;

    public async Task<Result<ChatSendMessageResult>> HandleAsync(SendChatMessageCommand command, CallContext context)
    {
        var req = new ChatSendRequest(command.UserMessage, command.ContextSummary, command.Model);
        // Note: for streaming progress the VM can still wire IProgress locally before/after dispatch if the service supports; here we do simple call.
        var res = await _chatService.SendMessageAsync(req, null, context.CancellationToken).ConfigureAwait(true);
        return Result<ChatSendMessageResult>.Success(res);
    }
}

/// <summary>Thin handler for opening agent config (fire-and-forget side effect).</summary>
internal sealed class OpenChatAgentConfigCommandHandler : ICommandHandler<OpenChatAgentConfigCommand, ChatFileOpenResult>
{
    private readonly IChatWindowService _chatService;
    public OpenChatAgentConfigCommandHandler(IChatWindowService chatService) => _chatService = chatService;
    public async Task<Result<ChatFileOpenResult>> HandleAsync(OpenChatAgentConfigCommand command, CallContext context)
    {
        var res = await _chatService.OpenAgentConfigAsync(context.CancellationToken).ConfigureAwait(true);
        return Result<ChatFileOpenResult>.Success(res);
    }
}

/// <summary>Thin handler for opening prompt templates.</summary>
internal sealed class OpenChatPromptTemplatesCommandHandler : ICommandHandler<OpenChatPromptTemplatesCommand, ChatFileOpenResult>
{
    private readonly IChatWindowService _chatService;
    public OpenChatPromptTemplatesCommandHandler(IChatWindowService chatService) => _chatService = chatService;
    public async Task<Result<ChatFileOpenResult>> HandleAsync(OpenChatPromptTemplatesCommand command, CallContext context)
    {
        var res = await _chatService.OpenPromptTemplatesAsync(context.CancellationToken).ConfigureAwait(true);
        return Result<ChatFileOpenResult>.Success(res);
    }
}
