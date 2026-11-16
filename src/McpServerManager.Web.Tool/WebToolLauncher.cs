using System.Diagnostics;
using System.Runtime.InteropServices;

namespace McpServerManager.Web.Tool;

/// <summary>
/// Identifies an operating system supported by the portable <c>mcp-web</c> launcher.
/// </summary>
public enum WebToolOperatingSystem
{
    /// <summary>
    /// Microsoft Windows.
    /// </summary>
    Windows,

    /// <summary>
    /// Linux.
    /// </summary>
    Linux,

    /// <summary>
    /// Apple macOS.
    /// </summary>
    MacOS,

    /// <summary>
    /// An operating system without a packaged Web UI payload.
    /// </summary>
    Other,
}

/// <summary>
/// Describes the packaged executable selected for a host platform.
/// </summary>
/// <param name="RuntimeIdentifier">The runtime identifier of the selected payload.</param>
/// <param name="ExecutablePath">The absolute path of the selected executable.</param>
/// <param name="RequiresUnixExecutePermission">Whether Unix execute bits must be ensured before launch.</param>
public sealed record WebToolPayload(
    string RuntimeIdentifier,
    string ExecutablePath,
    bool RequiresUnixExecutePermission);

/// <summary>
/// Selects and starts the platform-specific Web UI packaged with the global tool.
/// </summary>
public static class WebToolLauncher
{
    /// <summary>
    /// Starts the payload for the current operating system and forwards all tool arguments.
    /// </summary>
    /// <param name="arguments">Arguments supplied to the <c>mcp-web</c> command.</param>
    /// <returns>The exit code returned by the platform-specific Web UI process.</returns>
    public static int Run(IReadOnlyList<string> arguments)
    {
        var payload = ResolvePayload(
            AppContext.BaseDirectory,
            GetCurrentOperatingSystem(),
            RuntimeInformation.OSArchitecture);

        if (!File.Exists(payload.ExecutablePath))
        {
            throw new FileNotFoundException(
                $"The packaged {payload.RuntimeIdentifier} Web UI executable was not found.",
                payload.ExecutablePath);
        }

        if (payload.RequiresUnixExecutePermission)
        {
            EnsureUnixExecutePermission(payload.ExecutablePath);
        }

        return RunProcess(CreateStartInfo(payload, arguments));
    }

    /// <summary>
    /// Resolves the packaged payload for an operating system and architecture.
    /// </summary>
    /// <param name="baseDirectory">The directory containing the portable launcher.</param>
    /// <param name="operatingSystem">The operating system to resolve.</param>
    /// <param name="architecture">The operating-system architecture to resolve.</param>
    /// <returns>The matching packaged payload.</returns>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the package does not contain a payload for the requested platform.
    /// </exception>
    public static WebToolPayload ResolvePayload(
        string baseDirectory,
        WebToolOperatingSystem operatingSystem,
        Architecture architecture)
    {
        if (architecture != Architecture.X64)
        {
            throw CreatePlatformNotSupportedException(operatingSystem, architecture);
        }

        var (runtimeIdentifier, executableName, requiresUnixExecutePermission) = operatingSystem switch
        {
            WebToolOperatingSystem.Windows => ("win-x64", "McpServerManager.Web.Hybrid.exe", false),
            WebToolOperatingSystem.Linux => ("linux-x64", "McpServerManager.Web", true),
            _ => throw CreatePlatformNotSupportedException(operatingSystem, architecture),
        };

        var executablePath = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "runtimes",
            runtimeIdentifier,
            executableName));

        return new WebToolPayload(runtimeIdentifier, executablePath, requiresUnixExecutePermission);
    }

    /// <summary>
    /// Creates process start information that forwards arguments without shell parsing.
    /// </summary>
    /// <param name="payload">The payload to start.</param>
    /// <param name="arguments">Arguments to forward.</param>
    /// <returns>Configured process start information.</returns>
    public static ProcessStartInfo CreateStartInfo(
        WebToolPayload payload,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = payload.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// Starts a child process, waits for it to exit, and returns its exit code.
    /// </summary>
    /// <param name="startInfo">Process configuration to execute.</param>
    /// <returns>The child process exit code.</returns>
    public static int RunProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start '{startInfo.FileName}'.");
        process.WaitForExit();
        return process.ExitCode;
    }

    private static WebToolOperatingSystem GetCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return WebToolOperatingSystem.Windows;
        if (OperatingSystem.IsLinux())
            return WebToolOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return WebToolOperatingSystem.MacOS;
        return WebToolOperatingSystem.Other;
    }

    private static PlatformNotSupportedException CreatePlatformNotSupportedException(
        WebToolOperatingSystem operatingSystem,
        Architecture architecture)
        => new($"mcp-web does not contain a payload for {operatingSystem} {architecture}.");

    private static void EnsureUnixExecutePermission(string executablePath)
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mode = File.GetUnixFileMode(executablePath);
        var executeMode = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        if ((mode & executeMode) != executeMode)
        {
            File.SetUnixFileMode(executablePath, mode | executeMode);
        }
    }
}
