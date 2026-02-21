using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.Cqrs;

/// <summary>Simple in-process mediator for CQRS dispatch. Handlers are registered by type.</summary>
public interface IMediator
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResult>;
    Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>;

    /// <summary>True when any tracked background work is still running.</summary>
    bool IsBusy { get; }

    /// <summary>Track a fire-and-forget background task. The mediator sets IsBusy=true while any tracked tasks are outstanding.</summary>
    void TrackBackgroundWork(Task task);
}

public sealed class Mediator : IMediator
{
    private readonly Dictionary<Type, object> _handlers = new();
    private readonly object _busyLock = new();
    private readonly List<Task> _outstandingTasks = new();

    /// <summary>Raised on every IsBusy transition (true→false or false→true).</summary>
    public event Action<bool>? IsBusyChanged;

    public bool IsBusy
    {
        get { lock (_busyLock) return _outstandingTasks.Count > 0; }
    }

    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("Mediator");

    private static void Log(string message) => _logger.LogInformation(message);

    public void TrackBackgroundWork(Task task)
    {
        if (task == null || task.IsCompleted) return;
        bool wasBusy;
        int count;
        lock (_busyLock)
        {
            wasBusy = _outstandingTasks.Count > 0;
            _outstandingTasks.Add(task);
            count = _outstandingTasks.Count;
        }
        Log($"TrackBackgroundWork: added task, outstanding={count}, wasBusy={wasBusy}");
        if (!wasBusy)
            IsBusyChanged?.Invoke(true);

        task.ContinueWith(t =>
        {
            bool nowIdle;
            int remaining;
            lock (_busyLock)
            {
                _outstandingTasks.Remove(task);
                remaining = _outstandingTasks.Count;
                nowIdle = remaining == 0;
            }
            Log($"TrackBackgroundWork: task completed (faulted={t.IsFaulted}), outstanding={remaining}, nowIdle={nowIdle}");
            if (nowIdle)
                IsBusyChanged?.Invoke(false);
        }, TaskScheduler.Default);
    }

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
        Log($"SendAsync<{typeof(TCommand).Name}> dispatching...");
        var task = ((ICommandHandler<TCommand>)handler).ExecuteAsync(command, cancellationToken);
        task.ContinueWith(t => Log($"SendAsync<{typeof(TCommand).Name}> completed (faulted={t.IsFaulted})"), TaskScheduler.Default);
        return task;
    }

    public Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResult>
    {
        if (!_handlers.TryGetValue(typeof(TCommand), out var handler))
            throw new InvalidOperationException($"No handler registered for {typeof(TCommand).Name}");
        Log($"SendAsync<{typeof(TCommand).Name},{typeof(TResult).Name}> dispatching...");
        var task = ((ICommandHandler<TCommand, TResult>)handler).ExecuteAsync(command, cancellationToken);
        task.ContinueWith(t => Log($"SendAsync<{typeof(TCommand).Name},{typeof(TResult).Name}> completed (faulted={t.IsFaulted})"), TaskScheduler.Default);
        return task;
    }

    public Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>
    {
        if (!_handlers.TryGetValue(typeof(TQuery), out var handler))
            throw new InvalidOperationException($"No handler registered for {typeof(TQuery).Name}");
        Log($"QueryAsync<{typeof(TQuery).Name},{typeof(TResult).Name}> dispatching...");
        var task = ((IQueryHandler<TQuery, TResult>)handler).ExecuteAsync(query, cancellationToken);
        task.ContinueWith(t => Log($"QueryAsync<{typeof(TQuery).Name},{typeof(TResult).Name}> completed (faulted={t.IsFaulted})"), TaskScheduler.Default);
        return task;
    }
}
