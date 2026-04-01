using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.Core.Commands;
using UiPromptTemplate = McpServerManager.UI.Core.Models.PromptTemplate;
using UiChatFileOpenResult = McpServerManager.UI.Core.Services.ChatFileOpenResult;
using UiChatLoadModelsResult = McpServerManager.UI.Core.Services.ChatLoadModelsResult;
using UiChatPreparedPromptResult = McpServerManager.UI.Core.Services.ChatPreparedPromptResult;
using UiChatSendMessageResult = McpServerManager.UI.Core.Services.ChatSendMessageResult;
using UiChatSendRequest = McpServerManager.UI.Core.Services.ChatSendRequest;
using CorePromptTemplate = McpServerManager.Core.Models.PromptTemplate;
using CoreChatFileOpenResult = McpServerManager.Core.Services.ChatFileOpenResult;
using CoreChatLoadModelsResult = McpServerManager.Core.Commands.ChatLoadModelsResult;
using CoreChatPreparedPromptResult = McpServerManager.Core.Commands.ChatPreparedPromptResult;
using CoreChatSendMessageResult = McpServerManager.Core.Commands.ChatSendMessageResult;
using CoreChatSendRequest = McpServerManager.Core.Services.ChatSendRequest;

namespace McpServerManager.Core.Services;

/// <summary>
/// Adapts core CQRS chat commands/queries to UI.Core chat service abstraction.
/// </summary>
public sealed class ChatWindowServiceAdapter : IChatWindowService
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Creates a chat service adapter backed by a CQRS dispatcher.
    /// </summary>
    public ChatWindowServiceAdapter(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public async Task<UiChatFileOpenResult> OpenAgentConfigAsync(CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new ChatOpenAgentConfigCommand(), cancellationToken).ConfigureAwait(true);
        return result.IsSuccess && result.Value is not null
            ? Map(result.Value)
            : new UiChatFileOpenResult(false, null, result.Error ?? "Failed to open agent config");
    }

    /// <inheritdoc />
    public async Task<UiChatFileOpenResult> OpenPromptTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new ChatOpenPromptTemplatesCommand(), cancellationToken).ConfigureAwait(true);
        return result.IsSuccess && result.Value is not null
            ? Map(result.Value)
            : new UiChatFileOpenResult(false, null, result.Error ?? "Failed to open prompt templates");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UiPromptTemplate>> LoadPromptsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new ChatLoadPromptsCommand(), cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess || result.Value == null)
            return Array.Empty<UiPromptTemplate>();

        return result.Value.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<string> PopulatePromptAsync(UiPromptTemplate? prompt, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher
            .SendAsync(new ChatPopulatePromptCommand(Map(prompt)), cancellationToken)
            .ConfigureAwait(true);
        return result.IsSuccess ? (result.Value ?? string.Empty) : string.Empty;
    }

    /// <inheritdoc />
    public async Task<UiChatPreparedPromptResult> SubmitPromptAsync(UiPromptTemplate? prompt, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher
            .SendAsync(new ChatSubmitPromptCommand(Map(prompt)), cancellationToken)
            .ConfigureAwait(true);
        return result.IsSuccess && result.Value is not null
            ? Map(result.Value)
            : new UiChatPreparedPromptResult(false, string.Empty);
    }

    /// <inheritdoc />
    public async Task<UiChatLoadModelsResult> LoadModelsAsync(string? initialPreferredModel, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher
            .QueryAsync(new ChatLoadModelsQuery(initialPreferredModel), cancellationToken)
            .ConfigureAwait(true);
        return result.IsSuccess && result.Value is not null
            ? Map(result.Value)
            : new UiChatLoadModelsResult(false, Array.Empty<string>(), null);
    }

    /// <inheritdoc />
    public async Task<UiChatSendMessageResult> SendMessageAsync(
        UiChatSendRequest request,
        IProgress<string>? contentProgress = null,
        CancellationToken cancellationToken = default)
    {
        var coreRequest = new CoreChatSendRequest(
            request.UserMessage,
            request.ContextSummary,
            request.Model);

        var result = await _dispatcher
            .SendAsync(new ChatSendMessageCommand(coreRequest, contentProgress), cancellationToken)
            .ConfigureAwait(true);

        return result.IsSuccess && result.Value is not null
            ? Map(result.Value)
            : new UiChatSendMessageResult(false, string.Empty, false, result.Error ?? "Dispatch failed");
    }

    private static UiPromptTemplate Map(CorePromptTemplate source)
        => new()
        {
            Name = source.Name,
            Template = source.Template
        };

    private static CorePromptTemplate? Map(UiPromptTemplate? source)
        => source == null
            ? null
            : new CorePromptTemplate
            {
                Name = source.Name,
                Template = source.Template
            };

    private static UiChatFileOpenResult Map(CoreChatFileOpenResult source)
        => new(source.Opened, source.FilePath, source.ErrorMessage);

    private static UiChatPreparedPromptResult Map(CoreChatPreparedPromptResult source)
        => new(source.ShouldSend, source.PromptText);

    private static UiChatLoadModelsResult Map(CoreChatLoadModelsResult source)
        => new(source.IsReachable, source.Models, source.SelectedModel);

    private static UiChatSendMessageResult Map(CoreChatSendMessageResult source)
        => new(source.Success, source.ReplyText, source.WasCancelled, source.ErrorMessage);
}
