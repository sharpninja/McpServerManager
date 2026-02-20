using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RequestTracker.Core.Services;

/// <summary>
/// Invokes the GitHub Copilot CLI agent with a prompt, streaming stdout line-by-line.
/// Ported from the VS2026 extension (McpServer.VsExtension.McpTodo.Vsix/CopilotCliHelper.cs).
/// </summary>
public static class CopilotCliService
{
    private static readonly Regex AnsiEscapePattern = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    /// <summary>
    /// Invoke the Copilot CLI with the given prompt, streaming stdout line-by-line
    /// via <paramref name="onStdoutLine"/>. Supports cancellation.
    /// </summary>
    public static async Task<CopilotCliResult> InvokeAsync(
        string prompt,
        string? workingDirectory = null,
        Action<string>? onStdoutLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = 300_000)
    {
        string? tmpFile = null;
        try
        {
            // Write prompt to a temp file to avoid shell escaping issues
            tmpFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmpFile, prompt, cancellationToken).ConfigureAwait(false);

            var shell = "pwsh";
            var agentCmd = $"copilot -p \"$(Get-Content -Raw '{tmpFile.Replace("'", "''")}')\" --allow-all";
            var shellArgs = $"-NoProfile -Command \"{agentCmd}\"";

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                WorkingDirectory = workingDirectory ?? "",
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new CopilotCliResult { State = "spawnError", Body = "Process.Start returned null" };

            var stdoutBuf = new StringBuilder();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var wasCancelled = false;

            using (cancellationToken.Register(() =>
            {
                wasCancelled = true;
                TryKillProcess(process);
            }))
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    stdoutBuf.AppendLine(line);
                    onStdoutLine?.Invoke(AnsiEscapePattern.Replace(line, ""));
                }
            }

            var exited = process.WaitForExit(timeoutMs);
            var stderr = await stderrTask.ConfigureAwait(false);
            var stdout = stdoutBuf.ToString();

            if (!exited)
            {
                TryKillProcess(process);
                return new CopilotCliResult { State = "timeout", Body = stdout, Stderr = stderr };
            }

            string state;
            if (wasCancelled)
                state = "cancelled";
            else
                state = process.ExitCode == 0 ? "success" : "error";

            return new CopilotCliResult
            {
                State = state,
                Body = stdout,
                Stderr = stderr,
                ExitCode = process.ExitCode,
            };
        }
        catch (OperationCanceledException)
        {
            return new CopilotCliResult { State = "cancelled", Body = "" };
        }
        catch (Exception ex)
        {
            return new CopilotCliResult { State = "spawnError", Body = ex.Message };
        }
        finally
        {
            if (tmpFile != null)
                TryDeleteFile(tmpFile);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try { if (!process.HasExited) process.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed class CopilotCliResult
{
    public string State { get; set; } = "";
    public string Body { get; set; } = "";
    public string Stderr { get; set; } = "";
    public int? ExitCode { get; set; }
}
