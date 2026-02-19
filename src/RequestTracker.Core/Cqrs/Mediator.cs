using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RequestTracker.Core.Cqrs;

/// <summary>Simple in-process mediator for CQRS dispatch. Handlers are registered by type.</summary>
public interface IMediator
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResult>;
    Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>;
}

public sealed class Mediator : IMediator
{
    private readonly Dictionary<Type, object> _handlers = new();

    public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        => _handlers[typeof(TCommand)] = handler;

    public void Register<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler) where TCommand : ICommand<TResult>
        => _handlers[typeof(TCommand)] = handler;

    public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : IQuery<TResult>
        => _handlers[typeof(TQuery)] = handler;

    public Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        if (!_handlers.TryGetValue(typeof(TCommand), out var handler))
            throw new InvalidOperationException($"No handler registered for {typeof(TCommand).Name}");
        return ((ICommandHandler<TCommand>)handler).ExecuteAsync(command, cancellationToken);
    }

    public Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResult>
    {
        if (!_handlers.TryGetValue(typeof(TCommand), out var handler))
            throw new InvalidOperationException($"No handler registered for {typeof(TCommand).Name}");
        return ((ICommandHandler<TCommand, TResult>)handler).ExecuteAsync(command, cancellationToken);
    }

    public Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>
    {
        if (!_handlers.TryGetValue(typeof(TQuery), out var handler))
            throw new InvalidOperationException($"No handler registered for {typeof(TQuery).Name}");
        return ((IQueryHandler<TQuery, TResult>)handler).ExecuteAsync(query, cancellationToken);
    }
}
