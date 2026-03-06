using System;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServer.Cqrs.Mvvm;

namespace McpServerManager.Core.Commands;

public static class CqrsRelayFactory
{
    public static CqrsRelayCommand<bool> Create(Dispatcher dispatcher, Action action, Func<bool>? canExecute = null)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);

    public static CqrsRelayCommand<bool> Create(Dispatcher dispatcher, Func<Task> action, Func<bool>? canExecute = null)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);

    public static CqrsRelayCommand<bool> Create<T>(Dispatcher dispatcher, Action<T?> action, Func<bool>? canExecute = null)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);

    public static CqrsRelayCommand<bool> Create<T>(Dispatcher dispatcher, Action<T?> action, Func<T?, bool>? canExecute)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);

    public static CqrsRelayCommand<bool> Create<T>(Dispatcher dispatcher, Func<T?, Task> action, Func<bool>? canExecute = null)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);

    public static CqrsRelayCommand<bool> Create<T>(Dispatcher dispatcher, Func<T?, Task> action, Func<T?, bool>? canExecute)
        => McpServer.UI.Core.Commands.CqrsRelayFactory.Create(dispatcher, action, canExecute);
}

