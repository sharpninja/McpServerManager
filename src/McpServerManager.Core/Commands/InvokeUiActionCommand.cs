using System;
using System.Threading.Tasks;
using McpServer.Cqrs;

namespace McpServerManager.Core.Commands;

/// <summary>
/// Wraps a ViewModel action delegate so it can be dispatched through the CQRS dispatcher.
/// </summary>
public sealed record InvokeUiActionCommand(Func<Task> Action) : ICommand<bool>;

public sealed class InvokeUiActionHandler : ICommandHandler<InvokeUiActionCommand, bool>
{
    private readonly McpServer.UI.Core.Commands.InvokeUiActionHandler _sharedHandler = new();

    public Task<Result<bool>> HandleAsync(InvokeUiActionCommand command, CallContext context)
        => _sharedHandler.HandleAsync(new McpServer.UI.Core.Commands.InvokeUiActionCommand(command.Action), context);
}

