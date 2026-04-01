using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using CoreAppLogService = McpServerManager.Core.Services.AppLogService;
using UiCoreAppLogService = McpServerManager.UI.Core.Services.AppLogService;

namespace McpServerManager.Core.Tests.Integration;

public sealed class AppLogServiceBehaviorTests
{
    [Fact]
    public void CoreAppLogService_AddProvider_FansOutToAttachedProviders()
    {
        var service = CoreAppLogService.Instance;
        service.Clear();
        var provider = new RecordingLoggerProvider();

        service.AddProvider(provider);
        service.CreateLogger("core-test").LogInformation("core message");

        service.Entries.Should().ContainSingle(entry => entry.Message.Contains("core message", StringComparison.Ordinal));
        provider.Messages.Should().ContainSingle(message => message.Contains("core-test:core message", StringComparison.Ordinal));
    }

    [Fact]
    public void UiCoreAppLogService_AddProvider_FansOutToAttachedProviders()
    {
        var service = UiCoreAppLogService.Instance;
        service.Clear();
        var provider = new RecordingLoggerProvider();

        service.AddProvider(provider);
        service.CreateLogger("ui-core-test").LogInformation("ui-core message");

        service.Entries.Should().ContainSingle(entry => entry.Message.Contains("ui-core message", StringComparison.Ordinal));
        provider.Messages.Should().ContainSingle(message => message.Contains("ui-core-test:ui-core message", StringComparison.Ordinal));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages, categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(List<string> messages, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add($"{categoryName}:{formatter(state, exception)}");
        }
    }
}
