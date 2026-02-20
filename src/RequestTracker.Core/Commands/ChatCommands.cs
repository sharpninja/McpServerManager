using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using RequestTracker.Core.Cqrs;
using RequestTracker.Core.Models;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Core.Commands;

// --- Chat: Send Message ---

public sealed class ChatSendMessageCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatSendMessageCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatSendMessageHandler : ICommandHandler<ChatSendMessageCommand>
{
    public async Task ExecuteAsync(ChatSendMessageCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.SendAsyncInternal().ConfigureAwait(false);
    }
}

// --- Chat: Load Models ---

public sealed class ChatLoadModelsCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatLoadModelsCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatLoadModelsHandler : ICommandHandler<ChatLoadModelsCommand>
{
    public async Task ExecuteAsync(ChatLoadModelsCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.LoadModelsAsyncInternal().ConfigureAwait(false);
    }
}

// --- Chat: Cancel Send ---

public sealed class ChatCancelSendCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatCancelSendCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatCancelSendHandler : ICommandHandler<ChatCancelSendCommand>
{
    public Task ExecuteAsync(ChatCancelSendCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.CancelSend();
        return Task.CompletedTask;
    }
}

// --- Chat: Submit Prompt Template ---

public sealed class ChatSubmitPromptCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public PromptTemplate? Prompt { get; }
    public ChatSubmitPromptCommand(ChatWindowViewModel vm, PromptTemplate? prompt)
    {
        ViewModel = vm;
        Prompt = prompt;
    }
}

public sealed class ChatSubmitPromptHandler : ICommandHandler<ChatSubmitPromptCommand>
{
    public async Task ExecuteAsync(ChatSubmitPromptCommand command, CancellationToken cancellationToken = default)
    {
        await command.ViewModel.SubmitPromptAsyncInternal(command.Prompt).ConfigureAwait(false);
    }
}

// --- Chat: Populate Prompt ---

public sealed class ChatPopulatePromptCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public PromptTemplate? Prompt { get; }
    public ChatPopulatePromptCommand(ChatWindowViewModel vm, PromptTemplate? prompt)
    {
        ViewModel = vm;
        Prompt = prompt;
    }
}

public sealed class ChatPopulatePromptHandler : ICommandHandler<ChatPopulatePromptCommand>
{
    public Task ExecuteAsync(ChatPopulatePromptCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.PopulatePromptInternal(command.Prompt);
        return Task.CompletedTask;
    }
}

// --- Chat: Load Prompt Templates ---

public sealed class ChatLoadPromptsCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatLoadPromptsCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatLoadPromptsHandler : ICommandHandler<ChatLoadPromptsCommand>
{
    public Task ExecuteAsync(ChatLoadPromptsCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.LoadPromptsInternal();
        return Task.CompletedTask;
    }
}

// --- Chat: Open Agent Config ---

public sealed class ChatOpenAgentConfigCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatOpenAgentConfigCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatOpenAgentConfigHandler : ICommandHandler<ChatOpenAgentConfigCommand>
{
    public Task ExecuteAsync(ChatOpenAgentConfigCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenAgentConfigInternal();
        return Task.CompletedTask;
    }
}

// --- Chat: Open Prompt Templates File ---

public sealed class ChatOpenPromptTemplatesCommand : ICommand
{
    public ChatWindowViewModel ViewModel { get; }
    public ChatOpenPromptTemplatesCommand(ChatWindowViewModel vm) => ViewModel = vm;
}

public sealed class ChatOpenPromptTemplatesHandler : ICommandHandler<ChatOpenPromptTemplatesCommand>
{
    public Task ExecuteAsync(ChatOpenPromptTemplatesCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.OpenPromptTemplatesInternal();
        return Task.CompletedTask;
    }
}
