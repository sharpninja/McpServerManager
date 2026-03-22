using System;
using McpServer.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.UI.Core.Hosting;

public enum McpHostLifetimeStrategy
{
    Singleton,
    Scoped,
}

public static class McpHostLifetimeStrategyExtensions
{
    public static IServiceCollection AddDispatcherForHostLifetime(
        this IServiceCollection services,
        McpHostLifetimeStrategy lifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        return lifetime switch
        {
            McpHostLifetimeStrategy.Singleton => services.AddCqrsDispatcher(),
            McpHostLifetimeStrategy.Scoped => services
                .AddScoped<Dispatcher>()
                .AddScoped<IDispatcher>(sp => sp.GetRequiredService<Dispatcher>()),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null),
        };
    }
}
