using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace McpServerManager.Director;

/// <summary>
/// TR-MCP-LOG-001: Configures Serilog file-based logging for the Director CLI.
/// Terminal.Gui owns stdout/stderr, so all log output goes to a rolling file.
/// Registers a pre-built logger factory to avoid circular
/// dependency with the CQRS Dispatcher (which is both an <see cref="ILoggerProvider"/>
/// and depends on <see cref="ILogger{T}"/>).
/// </summary>
internal static class DirectorLogging
{
    private static readonly string s_logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpServer", "Director", "logs");

    /// <summary>Registers Serilog file logging with the service collection.</summary>
    public static IServiceCollection AddDirectorLogging(this IServiceCollection services)
    {
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(s_logDir, "director-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddSingleton<ILoggerProvider>(_ => new SerilogLoggerProvider(serilogLogger, dispose: true));

        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(sp => LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            foreach (var provider in sp.GetServices<ILoggerProvider>())
                builder.AddProvider(provider);
        }));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return services;
    }
}
