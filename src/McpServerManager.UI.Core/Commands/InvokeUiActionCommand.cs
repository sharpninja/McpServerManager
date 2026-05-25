using System;
using System.Threading.Tasks;
using McpServer.Cqrs;
using Newtonsoft.Json;

namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Wraps a ViewModel action delegate so it can be dispatched through the CQRS dispatcher.
/// </summary>
public sealed class InvokeUiActionCommand : ICommand<bool>
{
    /// <summary>
    /// Default Constructor
    /// </summary>
    /// <param name="Action"></param>
    public InvokeUiActionCommand(Func<Task> Action)
    {
        this.Action = Action;
    }

    /// <summary>
    /// Action yo execute on the UI thread. This is not serialized or sent over the wire, but is instead set by the caller before dispatching.
    /// </summary>
    [JsonIgnore]
    public Func<Task> Action { get; }
}

public sealed class InvokeUiActionHandler(Services.IUiDispatcherService uiDispatcher) : ICommandHandler<InvokeUiActionCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(InvokeUiActionCommand command, CallContext context)
    {
        if (uiDispatcher.CheckAccess())
        {
            await command.Action().ConfigureAwait(true);
            return Result<bool>.Success(true);
        }

        await uiDispatcher.InvokeAsync(command.Action).ConfigureAwait(true);
        return Result<bool>.Success(true);
    }
}

