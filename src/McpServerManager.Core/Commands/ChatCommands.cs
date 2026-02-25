using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.Commands;

public sealed record ChatPreparedPromptResult(bool ShouldSend, string PromptText);

public sealed record ChatLoadModelsResult(bool IsReachable, IReadOnlyList<string> Models, string? SelectedModel);
public sealed record ChatSendMessageResult(bool Success, string ReplyText, bool WasCancelled, string? ErrorMessage = null);

internal static class ChatPromptTemplateFormatter
{
    public static string GetPromptText(PromptTemplate? prompt)
    {
        if (prompt == null || string.IsNullOrEmpty(prompt.Template)) return "";
        return prompt.Template.Trim();
    }
}

// --- Chat: Open Agent Config ---

public sealed class ChatOpenAgentConfigCommand : ICommand<ChatFileOpenResult>
{
}

public sealed class ChatOpenAgentConfigHandler : ICommandHandler<ChatOpenAgentConfigCommand, ChatFileOpenResult>
{
    private readonly IChatConfigFilesService _filesService;
    public ChatOpenAgentConfigHandler(IChatConfigFilesService filesService) => _filesService = filesService;

    public Task<ChatFileOpenResult> ExecuteAsync(ChatOpenAgentConfigCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(_filesService.OpenAgentConfigInEditor());
}

// --- Chat: Open Prompt Templates File ---

public sealed class ChatOpenPromptTemplatesCommand : ICommand<ChatFileOpenResult>
{
}

public sealed class ChatOpenPromptTemplatesHandler : ICommandHandler<ChatOpenPromptTemplatesCommand, ChatFileOpenResult>
{
    private readonly IChatConfigFilesService _filesService;
    public ChatOpenPromptTemplatesHandler(IChatConfigFilesService filesService) => _filesService = filesService;

    public Task<ChatFileOpenResult> ExecuteAsync(ChatOpenPromptTemplatesCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(_filesService.OpenPromptTemplatesInEditor());
}

// --- Chat: Load Prompt Templates ---

public sealed class ChatLoadPromptsCommand : ICommand<IReadOnlyList<PromptTemplate>>
{
}

public sealed class ChatLoadPromptsHandler : ICommandHandler<ChatLoadPromptsCommand, IReadOnlyList<PromptTemplate>>
{
    private readonly IChatPromptTemplateService _promptTemplateService;
    public ChatLoadPromptsHandler(IChatPromptTemplateService promptTemplateService) => _promptTemplateService = promptTemplateService;

    public Task<IReadOnlyList<PromptTemplate>> ExecuteAsync(ChatLoadPromptsCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(_promptTemplateService.GetPromptTemplates());
}

// --- Chat: Submit Prompt Template ---

public sealed class ChatSubmitPromptCommand : ICommand<ChatPreparedPromptResult>
{
    public PromptTemplate? Prompt { get; }

    public ChatSubmitPromptCommand(PromptTemplate? prompt)
    {
        Prompt = prompt;
    }
}

public sealed class ChatSubmitPromptHandler : ICommandHandler<ChatSubmitPromptCommand, ChatPreparedPromptResult>
{
    public Task<ChatPreparedPromptResult> ExecuteAsync(ChatSubmitPromptCommand command, CancellationToken cancellationToken = default)
    {
        var text = ChatPromptTemplateFormatter.GetPromptText(command.Prompt);
        var shouldSend = !string.IsNullOrWhiteSpace(text);
        return Task.FromResult(new ChatPreparedPromptResult(shouldSend, text));
    }
}

// --- Chat: Populate Prompt ---

public sealed class ChatPopulatePromptCommand : ICommand<string>
{
    public PromptTemplate? Prompt { get; }
    public ChatPopulatePromptCommand(PromptTemplate? prompt) => Prompt = prompt;
}

public sealed class ChatPopulatePromptHandler : ICommandHandler<ChatPopulatePromptCommand, string>
{
    public Task<string> ExecuteAsync(ChatPopulatePromptCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(ChatPromptTemplateFormatter.GetPromptText(command.Prompt));
}

// --- Chat: Load Models (query) ---

public sealed class ChatLoadModelsQuery : IQuery<ChatLoadModelsResult>
{
    public string? InitialPreferredModel { get; }
    public ChatLoadModelsQuery(string? initialPreferredModel) => InitialPreferredModel = initialPreferredModel;
}

public sealed class ChatLoadModelsHandler : IQueryHandler<ChatLoadModelsQuery, ChatLoadModelsResult>
{
    private readonly IChatModelDiscoveryService _modelDiscoveryService;

    public ChatLoadModelsHandler(IChatModelDiscoveryService modelDiscoveryService)
        => _modelDiscoveryService = modelDiscoveryService;

    public async Task<ChatLoadModelsResult> ExecuteAsync(ChatLoadModelsQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _modelDiscoveryService.GetAvailableModelsAsync(cancellationToken).ConfigureAwait(true);
            var selected = !string.IsNullOrWhiteSpace(query.InitialPreferredModel) && models.Contains(query.InitialPreferredModel!)
                ? query.InitialPreferredModel
                : models.FirstOrDefault();

            return new ChatLoadModelsResult(true, models, selected);
        }
        catch
        {
            return new ChatLoadModelsResult(false, Array.Empty<string>(), null);
        }
    }
}

// --- Chat: Send Message ---

public sealed class ChatSendMessageCommand : ICommand<ChatSendMessageResult>
{
    public ChatSendRequest Request { get; }
    public IProgress<string>? ContentProgress { get; }

    public ChatSendMessageCommand(ChatSendRequest request, IProgress<string>? contentProgress = null)
    {
        Request = request;
        ContentProgress = contentProgress;
    }
}

public sealed class ChatSendMessageHandler : ICommandHandler<ChatSendMessageCommand, ChatSendMessageResult>
{
    private readonly IChatSendOrchestrationService _chatSendService;

    public ChatSendMessageHandler(IChatSendOrchestrationService chatSendService)
        => _chatSendService = chatSendService;

    public async Task<ChatSendMessageResult> ExecuteAsync(ChatSendMessageCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await _chatSendService.SendMessageAsync(command.Request, command.ContentProgress, cancellationToken).ConfigureAwait(true);
            return new ChatSendMessageResult(true, reply ?? "", false, null);
        }
        catch (OperationCanceledException)
        {
            return new ChatSendMessageResult(false, "[Cancelled]", true, null);
        }
        catch (Exception ex)
        {
            return new ChatSendMessageResult(false, "", false, ex.Message);
        }
    }
}
