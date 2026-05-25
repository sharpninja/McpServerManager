using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Application-wide logger that captures all log entries in memory.
/// Implements <see cref="ILoggerFactory"/> and <see cref="ILoggerProvider"/>
/// so it can be registered as the standard logging infrastructure.
/// </summary>
public sealed class AppLogService : ILoggerFactory, ILoggerProvider, McpServerManager.UI.Core.Services.IAppLogService
{
    private static readonly Lazy<AppLogService> _instance = new(() => new AppLogService());
    public static AppLogService Instance => _instance.Value;

    private readonly List<LogEntry> _entries = new();
    private readonly List<WeakReference<ILoggerProvider>> _providers = new();
    private readonly object _lock = new();
    private event Action<McpServerManager.UI.Core.Services.ILogEntry>? _entryAddedBridge;

    public event Action<LogEntry>? EntryAdded;

    event Action<McpServerManager.UI.Core.Services.ILogEntry>? McpServerManager.UI.Core.Services.IAppLogService.EntryAdded
    {
        add => _entryAddedBridge += value;
        remove => _entryAddedBridge -= value;
    }

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_lock) return _entries.ToArray(); }
    }

    IReadOnlyList<McpServerManager.UI.Core.Services.ILogEntry> McpServerManager.UI.Core.Services.IAppLogService.Entries => Entries;

    public void AddEntry(Microsoft.Extensions.Logging.LogLevel level, string source, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message);
        lock (_lock) _entries.Add(entry);
        EntryAdded?.Invoke(entry);
        _entryAddedBridge?.Invoke(entry);
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    public AppLogService ConfigureProviders(IEnumerable<ILoggerProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
            AddProvider(provider);

        return this;
    }

    // ILoggerFactory
    public ILogger CreateLogger(string categoryName) => new AppLogger(this, categoryName);
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (ReferenceEquals(provider, this))
            return;

        lock (_lock)
        {
            for (var index = _providers.Count - 1; index >= 0; index--)
            {
                if (!_providers[index].TryGetTarget(out var existingProvider))
                {
                    _providers.RemoveAt(index);
                    continue;
                }

                if (ReferenceEquals(existingProvider, provider))
                    return;
            }

            _providers.Add(new WeakReference<ILoggerProvider>(provider));
        }
    }

    // ILoggerProvider
    ILogger ILoggerProvider.CreateLogger(string categoryName) => new AppLogger(this, categoryName);

    public void Dispose() { }

    private AppLogService() { }

    internal ILoggerProvider[] GetProvidersSnapshot()
    {
        lock (_lock)
        {
            if (_providers.Count == 0)
                return [];

            var providers = new List<ILoggerProvider>(_providers.Count);
            for (var index = _providers.Count - 1; index >= 0; index--)
            {
                if (_providers[index].TryGetTarget(out var provider))
                {
                    providers.Add(provider);
                    continue;
                }

                _providers.RemoveAt(index);
            }

            providers.Reverse();
            return providers.ToArray();
        }
    }
}

/// <summary>
/// Per-category logger that forwards to <see cref="AppLogService"/>.
/// Also implements <see cref="ILogger{T}"/> via the non-generic <see cref="ILogger"/>.
/// </summary>
internal sealed class AppLogger : ILogger
{
    private readonly AppLogService _service;
    private readonly string _category;

    public AppLogger(AppLogService service, string category)
    {
        _service = service;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        List<IDisposable>? scopes = null;
        foreach (var provider in _service.GetProvidersSnapshot())
        {
            try
            {
                var scope = provider.CreateLogger(_category).BeginScope(state);
                if (scope is null)
                    continue;

                scopes ??= new List<IDisposable>();
                scopes.Add(scope);
            }
            catch
            {
            }
        }

        return scopes switch
        {
            null => null,
            { Count: 1 } => scopes[0],
            _ => new CompositeScope(scopes),
        };
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception != null)
            message += Environment.NewLine + exception;
        _service.AddEntry(logLevel, _category, message);

        foreach (var provider in _service.GetProvidersSnapshot())
        {
            try
            {
                provider.CreateLogger(_category).Log(logLevel, eventId, state, exception, formatter);
            }
            catch
            {
            }
        }
    }

    private sealed class CompositeScope : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _scopes;

        public CompositeScope(IReadOnlyList<IDisposable> scopes)
        {
            _scopes = scopes;
        }

        public void Dispose()
        {
            for (var index = _scopes.Count - 1; index >= 0; index--)
                _scopes[index].Dispose();
        }
    }
}

/// <summary>
/// Generic ILogger&lt;T&gt; implementation backed by <see cref="AppLogService"/>.
/// </summary>
public sealed class AppLogger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    public AppLogger(AppLogService service)
    {
        _inner = service.CreateLogger(typeof(T).Name);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}

public sealed class LogEntry : McpServerManager.UI.Core.Services.ILogEntry
{
    public DateTime Timestamp { get; }
    public Microsoft.Extensions.Logging.LogLevel Level { get; }
    public string Source { get; }
    public string Message { get; }
    public string Display => $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {(string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ")}{Message}";

    public LogEntry(DateTime timestamp, Microsoft.Extensions.Logging.LogLevel level, string source, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Source = source;
        Message = message;
    }
}
