using System.Net;
using System.Net.Sockets;

namespace McpServer.Web;

internal static class WebListenUrlSelector
{
    internal const int DefaultStartPort = 8901;

    private static readonly string[] EnvironmentVariableNames =
    [
        "ASPNETCORE_URLS",
        "URLS",
        "DOTNET_URLS",
    ];

    public static WebListenUrlSelection ResolveSelection(IReadOnlyList<string> args) =>
        ResolveSelection(args, Environment.GetEnvironmentVariable);

    internal static WebListenUrlSelection ResolveSelection(
        IReadOnlyList<string> args,
        Func<string, string?> environmentVariableReader)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environmentVariableReader);

        var explicitUrls = TryGetExplicitListenUrls(args, environmentVariableReader);
        if (!string.IsNullOrWhiteSpace(explicitUrls))
        {
            return new WebListenUrlSelection(explicitUrls, IsExplicit: true);
        }

        return new WebListenUrlSelection(FindAvailableLoopbackUrl(DefaultStartPort), IsExplicit: false);
    }

    internal static string? TryGetExplicitListenUrls(
        IReadOnlyList<string> args,
        Func<string, string?> environmentVariableReader)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environmentVariableReader);

        var commandLineUrls = TryGetUrlsFromArgs(args);
        if (!string.IsNullOrWhiteSpace(commandLineUrls))
        {
            return commandLineUrls;
        }

        foreach (var environmentVariableName in EnvironmentVariableNames)
        {
            var environmentUrl = environmentVariableReader(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentUrl))
            {
                return environmentUrl;
            }
        }

        return null;
    }

    internal static string? TryGetUrlsFromArgs(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        const string urlsSwitch = "--urls";
        const string urlsSwitchWithValuePrefix = "--urls=";

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument.Equals(urlsSwitch, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Count && !string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    return args[index + 1];
                }

                continue;
            }

            if (argument.StartsWith(urlsSwitchWithValuePrefix, StringComparison.OrdinalIgnoreCase) &&
                argument.Length > urlsSwitchWithValuePrefix.Length)
            {
                return argument[urlsSwitchWithValuePrefix.Length..];
            }
        }

        return null;
    }

    internal static string FindAvailableLoopbackUrl(int startPort)
    {
        if (startPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(startPort));
        }

        for (var port = startPort; port <= IPEndPoint.MaxPort; port++)
        {
            if (IsLoopbackPortAvailable(port))
            {
                return $"http://127.0.0.1:{port}";
            }
        }

        throw new InvalidOperationException($"No available loopback ports were found at or above {startPort}.");
    }

    internal static bool IsLoopbackPortAvailable(int port)
    {
        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            return false;
        }

        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}

internal sealed record WebListenUrlSelection(string Urls, bool IsExplicit);
