using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpServer.Cqrs;
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

public sealed record ChatOpenAgentConfigCommand() : McpServer.Cqrs.ICommand<ChatFileOpenResult>;

public sealed class ChatOpenAgentConfigHandler(IChatConfigFilesService filesService) : McpServer.Cqrs.ICommandHandler<ChatOpenAgentConfigCommand, ChatFileOpenResult>
{
    public Task<Result<ChatFileOpenResult>> HandleAsync(ChatOpenAgentConfigCommand command, CallContext context)
        => Task.FromResult(Result<ChatFileOpenResult>.Success(filesService.OpenAgentConfigInEditor()));
}

// --- Chat: Open Prompt Templates File ---

public sealed record ChatOpenPromptTemplatesCommand() : McpServer.Cqrs.ICommand<ChatFileOpenResult>;

public sealed class ChatOpenPromptTemplatesHandler(IChatConfigFilesService filesService) : McpServer.Cqrs.ICommandHandler<ChatOpenPromptTemplatesCommand, ChatFileOpenResult>
{
    public Task<Result<ChatFileOpenResult>> HandleAsync(ChatOpenPromptTemplatesCommand command, CallContext context)
        => Task.FromResult(Result<ChatFileOpenResult>.Success(filesService.OpenPromptTemplatesInEditor()));
}

// --- Chat: Load Prompt Templates ---

public sealed record ChatLoadPromptsCommand() : McpServer.Cqrs.ICommand<IReadOnlyList<PromptTemplate>>;

public sealed class ChatLoadPromptsHandler(IChatPromptTemplateService promptTemplateService) : McpServer.Cqrs.ICommandHandler<ChatLoadPromptsCommand, IReadOnlyList<PromptTemplate>>
{
    public Task<Result<IReadOnlyList<PromptTemplate>>> HandleAsync(ChatLoadPromptsCommand command, CallContext context)
        => Task.FromResult(Result<IReadOnlyList<PromptTemplate>>.Success(promptTemplateService.GetPromptTemplates()));
}

// --- Chat: Submit Prompt Template ---

public sealed record ChatSubmitPromptCommand(PromptTemplate? Prompt) : McpServer.Cqrs.ICommand<ChatPreparedPromptResult>;

public sealed class ChatSubmitPromptHandler : McpServer.Cqrs.ICommandHandler<ChatSubmitPromptCommand, ChatPreparedPromptResult>
{
    public Task<Result<ChatPreparedPromptResult>> HandleAsync(ChatSubmitPromptCommand command, CallContext context)
    {
        var text = ChatPromptTemplateFormatter.GetPromptText(command.Prompt);
        var shouldSend = !string.IsNullOrWhiteSpace(text);
        return Task.FromResult(Result<ChatPreparedPromptResult>.Success(new ChatPreparedPromptResult(shouldSend, text)));
    }
}

// --- Chat: Populate Prompt ---

public sealed record ChatPopulatePromptCommand(PromptTemplate? Prompt) : McpServer.Cqrs.ICommand<string>;

public sealed class ChatPopulatePromptHandler : McpServer.Cqrs.ICommandHandler<ChatPopulatePromptCommand, string>
{
    public Task<Result<string>> HandleAsync(ChatPopulatePromptCommand command, CallContext context)
        => Task.FromResult(Result<string>.Success(ChatPromptTemplateFormatter.GetPromptText(command.Prompt)));
}

// --- Chat: Load Models (query) ---

public sealed record ChatLoadModelsQuery(string? InitialPreferredModel) : McpServer.Cqrs.IQuery<ChatLoadModelsResult>;

public sealed class ChatLoadModelsHandler(IChatModelDiscoveryService modelDiscoveryService) : McpServer.Cqrs.IQueryHandler<ChatLoadModelsQuery, ChatLoadModelsResult>
{
    public async Task<Result<ChatLoadModelsResult>> HandleAsync(ChatLoadModelsQuery query, CallContext context)
    {
        try
        {
            var models = await modelDiscoveryService.GetAvailableModelsAsync(context.CancellationToken).ConfigureAwait(true);
            var selected = !string.IsNullOrWhiteSpace(query.InitialPreferredModel) && models.Contains(query.InitialPreferredModel!)
                ? query.InitialPreferredModel
                : models.FirstOrDefault();

            return Result<ChatLoadModelsResult>.Success(new ChatLoadModelsResult(true, models, selected));
        }
        catch
        {
            return Result<ChatLoadModelsResult>.Success(new ChatLoadModelsResult(false, Array.Empty<string>(), null));
        }
    }
}

// --- Chat: Send Message ---

public sealed record ChatSendMessageCommand(ChatSendRequest Request, IProgress<string>? ContentProgress = null) : McpServer.Cqrs.ICommand<ChatSendMessageResult>;

public sealed class ChatSendMessageHandler(IChatSendOrchestrationService chatSendService) : McpServer.Cqrs.ICommandHandler<ChatSendMessageCommand, ChatSendMessageResult>
{
    public async Task<Result<ChatSendMessageResult>> HandleAsync(ChatSendMessageCommand command, CallContext context)
    {
        try
        {
            var reply = await chatSendService.SendMessageAsync(command.Request, command.ContentProgress, context.CancellationToken).ConfigureAwait(true);
            return Result<ChatSendMessageResult>.Success(new ChatSendMessageResult(true, reply ?? "", false, null));
        }
        catch (OperationCanceledException)
        {
            return Result<ChatSendMessageResult>.Success(new ChatSendMessageResult(false, "[Cancelled]", true, null));
        }
        catch (Exception ex)
        {
            return Result<ChatSendMessageResult>.Failure(ex);
        }
    }
}

