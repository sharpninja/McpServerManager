using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.UI.Core.Commands;

public sealed record StartAgentEventListenerCommand(bool Restart = false) : ICommand<bool>;

public sealed class StartAgentEventListenerHandler(AgentEventListenerCoordinator coordinator)
    : ICommandHandler<StartAgentEventListenerCommand, bool>
{
    public Task<Result<bool>> HandleAsync(StartAgentEventListenerCommand command, CallContext context)
    {
        coordinator.Start(command.Restart);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed record StopAgentEventListenerCommand() : ICommand<bool>;

public sealed class StopAgentEventListenerHandler(AgentEventListenerCoordinator coordinator)
    : ICommandHandler<StopAgentEventListenerCommand, bool>
{
    public Task<Result<bool>> HandleAsync(StopAgentEventListenerCommand command, CallContext context)
    {
        coordinator.Stop();
        return Task.FromResult(Result<bool>.Success(true));
    }
}
