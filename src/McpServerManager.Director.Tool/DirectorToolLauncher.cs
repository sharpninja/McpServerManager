using System.Diagnostics;
using System.Runtime.InteropServices;

namespace McpServerManager.Director.Tool;

/// <summary>
/// Identifies an operating system supported by the portable <c>director</c> launcher.
/// </summary>
public enum DirectorToolOperatingSystem
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
    /// An operating system without a packaged Director payload.
    /// </summary>
    Other,
}

/// <summary>
/// Describes the packaged executable selected for a host platform.
/// </summary>
/// <param name="RuntimeIdentifier">The runtime identifier of the selected payload.</param>
/// <param name="ExecutablePath">The absolute path of the selected executable.</param>
/// <param name="RequiresUnixExecutePermission">Whether Unix execute bits must be ensured before launch.</param>
public sealed record DirectorToolPayload(
    string RuntimeIdentifier,
    string ExecutablePath,
    bool RequiresUnixExecutePermission);

/// <summary>
/// Selects and starts the platform-specific Director executable packaged with the global tool.
/// </summary>
/// <remarks>
/// Change log: 2026-11-16 — FR-DIRECTOR-001, TR-DIRECTOR-TOOL-001, TEST-DIRECTOR-001;
/// AC-DIRECTOR-PACKAGE, AC-DIRECTOR-LAUNCH, AC-DIRECTOR-WINDOWS-LINUX,
/// AC-DIRECTOR-UNSUPPORTED, AC-DIRECTOR-UNIX-MODE, AC-DIRECTOR-TESTS, and
/// AC-DIRECTOR-DEPLOY. Added host-platform selection so one installed command can
/// launch the packaged Windows or Linux Director executable without shell re-parsing.
/// </remarks>
public static class DirectorToolLauncher
{
    /// <summary>
    /// Starts the payload for the current operating system and forwards all tool arguments.
    /// </summary>
    /// <param name="arguments">Arguments supplied to the <c>director</c> command.</param>
    /// <returns>The exit code returned by the platform-specific Director process.</returns>
    public static int Run(IReadOnlyList<string> arguments)
    {
        var payload = ResolvePayload(
            AppContext.BaseDirectory,
            GetCurrentOperatingSystem(),
            RuntimeInformation.OSArchitecture);

        if (!File.Exists(payload.ExecutablePath))
        {
            throw new FileNotFoundException(
                $"The packaged {payload.RuntimeIdentifier} Director executable was not found.",
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
    public static DirectorToolPayload ResolvePayload(
        string baseDirectory,
        DirectorToolOperatingSystem operatingSystem,
        Architecture architecture)
    {
        if (architecture != Architecture.X64)
        {
            throw CreatePlatformNotSupportedException(operatingSystem, architecture);
        }

        var (runtimeIdentifier, executableName, requiresUnixExecutePermission) = operatingSystem switch
        {
            DirectorToolOperatingSystem.Windows => ("win-x64", "director.exe", false),
            DirectorToolOperatingSystem.Linux => ("linux-x64", "director", true),
            _ => throw CreatePlatformNotSupportedException(operatingSystem, architecture),
        };

        var executablePath = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "runtimes",
            runtimeIdentifier,
            executableName));

        return new DirectorToolPayload(runtimeIdentifier, executablePath, requiresUnixExecutePermission);
    }

    /// <summary>
    /// Creates process start information that forwards arguments without shell parsing.
    /// </summary>
    /// <param name="payload">The payload to start.</param>
    /// <param name="arguments">Arguments to forward.</param>
    /// <returns>Configured process start information.</returns>
    public static ProcessStartInfo CreateStartInfo(
        DirectorToolPayload payload,
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

    private static DirectorToolOperatingSystem GetCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return DirectorToolOperatingSystem.Windows;
        if (OperatingSystem.IsLinux())
            return DirectorToolOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return DirectorToolOperatingSystem.MacOS;
        return DirectorToolOperatingSystem.Other;
    }

    private static PlatformNotSupportedException CreatePlatformNotSupportedException(
        DirectorToolOperatingSystem operatingSystem,
        Architecture architecture)
        => new($"director does not contain a payload for {operatingSystem} {architecture}.");

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
