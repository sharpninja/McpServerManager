using System.Threading;
using System.Threading.Tasks;

namespace McpServerManager.Core.Cqrs;

/// <summary>Marker interface for CQRS commands (no result).</summary>
public interface ICommand { }

/// <summary>Marker interface for CQRS commands that return a result.</summary>
public interface ICommand<TResult> { }

/// <summary>Handles a CQRS command with no result.</summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a CQRS command that returns a result.</summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Marker interface for CQRS queries.</summary>
public interface IQuery<TResult> { }

/// <summary>Handles a CQRS query.</summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken = default);
}
