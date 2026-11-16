namespace McpServerManager.Director.Tool;

/// <summary>
/// Provides the portable <c>director</c> dotnet-tool entry point.
/// </summary>
/// <remarks>
/// Change log: 2026-11-16 — FR-DIRECTOR-001 and TR-DIRECTOR-TOOL-001;
/// AC-DIRECTOR-LAUNCH. The tool entry point now delegates to the platform-selecting
/// launcher and reports launch failures with a nonzero exit code.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Selects and runs the Director executable for the current host platform.
    /// </summary>
    /// <param name="args">Arguments to forward to Director.</param>
    /// <returns>The Director process exit code, or one when launch fails.</returns>
    public static int Main(string[] args)
    {
        try
        {
            return DirectorToolLauncher.Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
