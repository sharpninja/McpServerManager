using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Nuke.Common.Logger;

partial class Build
{
    private sealed class ProcessInvocationResult
    {
        public string FileName { get; init; } = string.Empty;
        public List<string> Arguments { get; } = new();
        public string WorkingDirectory { get; init; } = string.Empty;
        public int ExitCode { get; set; }
        public List<string> StandardOutputLines { get; } = new();
        public List<string> StandardErrorLines { get; } = new();

        public string GetCombinedOutput()
        {
            var combinedLines = new List<string>();
            combinedLines.AddRange(StandardOutputLines);
            combinedLines.AddRange(StandardErrorLines);
            return string.Join(Environment.NewLine, combinedLines);
        }
    }

    private sealed class VersionDetails
    {
        public string SemVer { get; init; } = "0.1.0";
        public int Major { get; init; }
        public int Minor { get; init; }
        public int Patch { get; init; }
        public int CommitsSinceVersionSource { get; init; }
        public int VersionCode { get; init; }
    }

    private sealed class AndroidDeviceInfo
    {
        public string Serial { get; init; } = string.Empty;
        public bool IsEmulator { get; init; }
        public string RawLine { get; init; } = string.Empty;
    }

    private sealed class AndroidResolutionResult
    {
        public string Status { get; init; } = "Skipped";
        public string Serial { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    private sealed class DeploymentResult
    {
        public string Target { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    private sealed class WebProcessInfo
    {
        public int ProcessId { get; init; }
        public int ParentProcessId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string CommandLine { get; init; } = string.Empty;
    }

    private string RepoRootPath => RootDirectory.ToString();
    private string ScriptsDirectoryPath => Path.Combine(RepoRootPath, "scripts");
    private string BuildDirectoryPath => Path.Combine(RepoRootPath, "build");
    private string ArtifactsDirectoryPath => Path.Combine(RepoRootPath, "artifacts");
    private string LogsDirectoryPath => Path.Combine(RepoRootPath, "logs");

    private static List<string> SplitLines(string content)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(content))
        {
            return lines;
        }

        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (rawLine.Length == 0)
            {
                continue;
            }

            lines.Add(rawLine);
        }

        return lines;
    }

    private static string QuotePowerShellLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string QuoteBashLiteral(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private void EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
    }

    private void ClearDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            return;
        }

        var directoryInfo = new DirectoryInfo(directoryPath);
        foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            file.IsReadOnly = false;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, true);
            }
            else if (File.Exists(entry))
            {
                File.Delete(entry);
            }
        }
    }

    private string ResolveOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(OutputDir))
        {
            return Path.IsPathRooted(OutputDir) ? OutputDir : Path.Combine(RepoRootPath, OutputDir);
        }

        return ArtifactsDirectoryPath;
    }

    private ProcessInvocationResult InvokeProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool throwOnFailure,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var result = new ProcessInvocationResult
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory
        };
        result.Arguments.AddRange(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        result.ExitCode = process.ExitCode;
        result.StandardOutputLines.AddRange(SplitLines(stdoutTask.Result));
        result.StandardErrorLines.AddRange(SplitLines(stderrTask.Result));

        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} exited with code {result.ExitCode}.{Environment.NewLine}{result.GetCombinedOutput()}");
        }

        return result;
    }

    private ProcessInvocationResult InvokeInteractiveProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool throwOnFailure,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var result = new ProcessInvocationResult
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory
        };
        result.Arguments.AddRange(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();

        result.ExitCode = process.ExitCode;

        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} exited with code {result.ExitCode}.");
        }

        return result;
    }

    private ProcessInvocationResult InvokePowerShellCommand(string scriptText, bool throwOnFailure = true)
    {
        var executable = ResolvePowerShellExecutable();
        return InvokeProcess(
            executable,
            new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                scriptText
            },
            RepoRootPath,
            throwOnFailure);
    }

    private string ResolvePowerShellExecutable()
    {
        if (CommandExists("pwsh"))
        {
            return "pwsh";
        }

        if (OperatingSystem.IsWindows())
        {
            return "powershell";
        }

        return "pwsh";
    }

    private ProcessInvocationResult InvokeDotNet(IReadOnlyList<string> arguments, string workingDirectory, bool throwOnFailure = true)
    {
        return InvokeProcess("dotnet", arguments, workingDirectory, throwOnFailure);
    }

    private ProcessInvocationResult InvokeGit(IReadOnlyList<string> arguments, bool throwOnFailure = true)
    {
        return InvokeProcess("git", arguments, RepoRootPath, throwOnFailure);
    }

    private bool CommandExists(string commandName)
    {
        try
        {
            var locator = OperatingSystem.IsWindows() ? "where.exe" : "which";
            var result = InvokeProcess(locator, new List<string> { commandName }, RepoRootPath, false);
            return result.ExitCode == 0 && result.StandardOutputLines.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private VersionDetails ResolveVersionDetails(string requestedVersion = "")
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            return ParseVersionDetails(requestedVersion, 0);
        }

        try
        {
            var result = InvokeDotNet(new List<string> { "tool", "run", "dotnet-gitversion", "/output", "json" }, RepoRootPath, false);
            if (result.ExitCode == 0)
            {
                var json = string.Join(Environment.NewLine, result.StandardOutputLines);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    var semVer = root.TryGetProperty("SemVer", out var semVerProperty)
                        ? semVerProperty.GetString() ?? "0.1.0"
                        : "0.1.0";
                    var commits = root.TryGetProperty("CommitsSinceVersionSource", out var commitsProperty) &&
                                  commitsProperty.TryGetInt32(out var parsedCommits)
                        ? parsedCommits
                        : 0;

                    return ParseVersionDetails(semVer, commits);
                }
            }
        }
        catch
        {
        }

        var gitVersionPath = Path.Combine(RepoRootPath, "GitVersion.yml");
        if (File.Exists(gitVersionPath))
        {
            var content = File.ReadAllText(gitVersionPath);
            var match = Regex.Match(content, @"(?m)^next-version:\s*(\d+\.\d+\.\d+)");
            if (match.Success)
            {
                return ParseVersionDetails(match.Groups[1].Value, 0);
            }
        }

        try
        {
            var gitDescribe = InvokeGit(new List<string> { "describe", "--tags", "--abbrev=0" }, false);
            if (gitDescribe.ExitCode == 0 && gitDescribe.StandardOutputLines.Count > 0)
            {
                var tag = gitDescribe.StandardOutputLines[0].Trim();
                return ParseVersionDetails(tag.TrimStart('v', 'V'), 0);
            }
        }
        catch
        {
        }

        return ParseVersionDetails("0.1.0", 0);
    }

    private VersionDetails ParseVersionDetails(string versionText, int commitsSinceVersionSource)
    {
        var prereleasePart = string.Empty;
        var versionPart = versionText;
        var hyphenIndex = versionText.IndexOf('-', StringComparison.Ordinal);
        if (hyphenIndex >= 0)
        {
            versionPart = versionText[..hyphenIndex];
            prereleasePart = versionText[(hyphenIndex + 1)..];
        }

        if (commitsSinceVersionSource == 0 && !string.IsNullOrWhiteSpace(prereleasePart))
        {
            var firstPrereleaseSegment = prereleasePart.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (int.TryParse(firstPrereleaseSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPrereleaseCommits))
            {
                commitsSinceVersionSource = parsedPrereleaseCommits;
            }
        }

        var segments = versionPart.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var major = segments.Length > 0 && int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMajor)
            ? parsedMajor
            : 0;
        var minor = segments.Length > 1 && int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMinor)
            ? parsedMinor
            : 0;
        var patch = segments.Length > 2 && int.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPatch)
            ? parsedPatch
            : 0;

        return new VersionDetails
        {
            SemVer = versionText,
            Major = major,
            Minor = minor,
            Patch = patch,
            CommitsSinceVersionSource = commitsSinceVersionSource,
            VersionCode = Math.Max(1, major * 10000 + minor * 1000 + patch * 100 + commitsSinceVersionSource)
        };
    }

    private string ResolveProjectPathOrDefault(string configuredPath, string defaultRelativePath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath) ? defaultRelativePath : configuredPath;
        return Path.IsPathRooted(candidate) ? candidate : Path.Combine(RepoRootPath, candidate);
    }

    private string ResolveTargetFramework(string projectPath, string fallbackTargetFramework)
    {
        var document = XDocument.Load(projectPath);
        var targetFrameworkElement = document
            .Descendants()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, "TargetFramework", StringComparison.OrdinalIgnoreCase));

        if (targetFrameworkElement is not null && !string.IsNullOrWhiteSpace(targetFrameworkElement.Value))
        {
            return targetFrameworkElement.Value.Trim();
        }

        return fallbackTargetFramework;
    }

    private void ApplyMarkdownAvaloniaLinuxPatchIfNeeded()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var propsPath = Path.Combine(RepoRootPath, "lib", "Markdown.Avalonia", "Markdown.Avalonia.props");
        if (!File.Exists(propsPath))
        {
            return;
        }

        var content = File.ReadAllText(propsPath);
        if (content.Contains("PackageTargetFrameworks>netstandard2.0</PackageTargetFrameworks>", StringComparison.Ordinal))
        {
            return;
        }

        const string patchBlock = """
  <PropertyGroup Condition=" '$(OS)' != 'Windows_NT' ">
    <PackageTargetFrameworks>netstandard2.0</PackageTargetFrameworks>
    <DemoAppTargetFrameworks>netstandard2.0</DemoAppTargetFrameworks>
    <TestTargetFrameworks>netstandard2.0</TestTargetFrameworks>
  </PropertyGroup>
""";

        var updated = content.Replace("</Project>", patchBlock + Environment.NewLine + "</Project>", StringComparison.Ordinal);
        File.WriteAllText(propsPath, updated);
    }

    private string ResolveAdbPath()
    {
        // Check if adb is already on the system PATH.
        if (CommandExists("adb"))
            return "adb";

        // Try ANDROID_HOME / ANDROID_SDK_ROOT environment variables.
        foreach (var envVar in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var sdkRoot = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(sdkRoot))
            {
                var candidate = Path.Combine(sdkRoot, "platform-tools", "adb.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Try the default Windows SDK install location.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var candidate = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        // Fallback — let the caller handle the missing-adb error.
        return "adb";
    }

    private List<AndroidDeviceInfo> GetAndroidDevicesCore()
    {
        var devices = new List<AndroidDeviceInfo>();
        var adbCheck = InvokeProcess(ResolveAdbPath(), new List<string> { "devices", "-l" }, RepoRootPath, false);
        if (adbCheck.ExitCode != 0)
        {
            return devices;
        }

        foreach (var line in adbCheck.StandardOutputLines)
        {
            var match = Regex.Match(line, @"^(?<serial>\S+)\s+device\b");
            if (!match.Success)
            {
                continue;
            }

            var serial = match.Groups["serial"].Value;
            devices.Add(new AndroidDeviceInfo
            {
                Serial = serial,
                IsEmulator = serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase),
                RawLine = line.Trim()
            });
        }

        return devices;
    }

    private AndroidResolutionResult ResolveAndroidDevice(bool expectEmulator, string requestedSerial)
    {
        var devices = GetAndroidDevicesCore();
        if (devices.Count == 0)
        {
            return new AndroidResolutionResult
            {
                Status = "Skipped",
                Serial = string.Empty,
                Message = "adb was not found or no Android devices are currently attached."
            };
        }

        if (!string.IsNullOrWhiteSpace(requestedSerial))
        {
            var exactMatch = devices.FirstOrDefault(x => string.Equals(x.Serial, requestedSerial, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is null)
            {
                return new AndroidResolutionResult
                {
                    Status = "Skipped",
                    Serial = string.Empty,
                    Message = $"Requested serial '{requestedSerial}' is not attached."
                };
            }

            return new AndroidResolutionResult
            {
                Status = "Success",
                Serial = exactMatch.Serial,
                Message = $"Using requested serial '{exactMatch.Serial}'."
            };
        }

        var candidate = devices.FirstOrDefault(x => x.IsEmulator == expectEmulator);
        if (candidate is null)
        {
            return new AndroidResolutionResult
            {
                Status = "Skipped",
                Serial = string.Empty,
                Message = expectEmulator ? "No attached emulator was detected." : "No attached physical Android device was detected."
            };
        }

        return new AndroidResolutionResult
        {
            Status = "Success",
            Serial = candidate.Serial,
            Message = $"Using detected serial '{candidate.Serial}'."
        };
    }

    private List<string> GetWslDistrosCore()
    {
        var distros = new List<string>();
        var result = InvokeProcess("wsl.exe", new List<string> { "-l", "-q" }, RepoRootPath, false);
        if (result.ExitCode != 0)
        {
            return distros;
        }

        foreach (var line in result.StandardOutputLines)
        {
            var normalized = line
                .Replace("\0", string.Empty, StringComparison.Ordinal)
                .Replace("\uFEFF", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            distros.Add(normalized);
        }

        return distros;
    }

    private string ResolveWslDistroName(string requestedDistro)
    {
        var distros = GetWslDistrosCore();
        if (distros.Count == 0)
        {
            throw new InvalidOperationException("WSL is not installed or no distros are available.");
        }

        if (!string.IsNullOrWhiteSpace(requestedDistro))
        {
            if (distros.Any(x => string.Equals(x, requestedDistro, StringComparison.OrdinalIgnoreCase)))
            {
                return requestedDistro;
            }

            throw new InvalidOperationException($"Requested WSL distro '{requestedDistro}' was not found.");
        }

        var preferred = distros.FirstOrDefault(x => !x.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase));
        return preferred ?? distros[0];
    }

    private ProcessInvocationResult InvokeWslCommand(string distro, string bashCommand, bool throwOnFailure = true)
    {
        return InvokeProcess(
            "wsl.exe",
            new List<string> { "-d", distro, "--", "bash", "-lc", bashCommand },
            RepoRootPath,
            throwOnFailure);
    }

    private ProcessInvocationResult InvokeInteractiveWslCommand(string distro, string bashCommand, bool throwOnFailure = true)
    {
        return InvokeInteractiveProcess(
            "wsl.exe",
            new List<string> { "-d", distro, "--", "bash", "-lc", bashCommand },
            RepoRootPath,
            throwOnFailure);
    }

    private string ConvertToWslPath(string distro, string windowsPath)
    {
        var bashCommand = $"wslpath -a {QuoteBashLiteral(windowsPath)}";
        var result = InvokeWslCommand(distro, bashCommand, false);
        if (result.ExitCode != 0 || result.StandardOutputLines.Count == 0)
        {
            throw new InvalidOperationException($"Failed to convert '{windowsPath}' to a WSL path. {result.GetCombinedOutput()}");
        }

        return result.StandardOutputLines[^1].Trim();
    }

    private bool IsWindowsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private string ResolveNuGetPackageRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("NuGetPackageRoot");
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
        {
            return envRoot;
        }

        var result = InvokeDotNet(new List<string> { "nuget", "locals", "global-packages", "--list" }, RepoRootPath, false);
        foreach (var line in result.StandardOutputLines)
        {
            if (line.Contains("global-packages:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    private string ResolveLatestVsix(string preferredPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            var absolute = Path.IsPathRooted(preferredPath) ? preferredPath : Path.Combine(RepoRootPath, preferredPath);
            if (!File.Exists(absolute))
            {
                throw new FileNotFoundException($"VSIX not found at '{absolute}'.");
            }

            return absolute;
        }

        var candidates = Directory
            .EnumerateFiles(RepoRootPath, "*.vsix", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        if (candidates.Count == 0)
        {
            throw new FileNotFoundException("No .vsix files were found under the repository.");
        }

        return candidates[0];
    }

    private static List<string> TailLines(string path, int lineCount)
    {
        var lines = new List<string>();
        if (!File.Exists(path))
        {
            return lines;
        }

        var allLines = File.ReadAllLines(path);
        var startIndex = Math.Max(0, allLines.Length - lineCount);
        for (var index = startIndex; index < allLines.Length; index++)
        {
            lines.Add(allLines[index]);
        }

        return lines;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private List<WebProcessInfo> QueryWebProcesses()
    {
        var processes = new List<WebProcessInfo>();
        if (!OperatingSystem.IsWindows())
        {
            return processes;
        }

        using var searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name, CommandLine FROM Win32_Process");
        foreach (var item in searcher.Get().OfType<ManagementObject>())
        {
            var name = item["Name"]?.ToString() ?? string.Empty;
            var commandLine = item["CommandLine"]?.ToString() ?? string.Empty;
            if (!(string.Equals(name, "dotnet.exe", StringComparison.OrdinalIgnoreCase) && commandLine.Contains("McpServer.Web", StringComparison.OrdinalIgnoreCase)) &&
                !(string.Equals(name, "McpServer.Web.exe", StringComparison.OrdinalIgnoreCase) && commandLine.Contains("McpServer.Web", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            processes.Add(new WebProcessInfo
            {
                ProcessId = Convert.ToInt32(item["ProcessId"] ?? 0, CultureInfo.InvariantCulture),
                ParentProcessId = Convert.ToInt32(item["ParentProcessId"] ?? 0, CultureInfo.InvariantCulture),
                Name = name,
                CommandLine = commandLine
            });
        }

        return processes;
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }

    private void WriteJsonToConsole<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        if (!string.IsNullOrWhiteSpace(JsonOutputPath))
        {
            var jsonOutputPath = Path.IsPathRooted(JsonOutputPath)
                ? JsonOutputPath
                : Path.Combine(RepoRootPath, JsonOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonOutputPath)!);
            File.WriteAllText(jsonOutputPath, json, new UTF8Encoding(false));
        }

        Console.WriteLine(json);
    }
}
