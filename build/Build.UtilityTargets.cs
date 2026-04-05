using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using static Nuke.Common.Logger;

partial class Build
{
    private string ResolveDefaultFdroidRepoUrl()
    {
        if (!string.IsNullOrWhiteSpace(ExpectedRepoUrl))
        {
            return ExpectedRepoUrl;
        }

        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? string.Empty;
        var owner = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_OWNER") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(repository) && !string.IsNullOrWhiteSpace(owner))
        {
            var repoName = repository.Contains('/') ? repository.Split('/')[1] : repository;
            return $"https://{owner}.github.io/{repoName}/repo";
        }

        return "https://localhost/repo";
    }

    private string ResolveVsixInstallDirectory()
    {
        if (!string.IsNullOrWhiteSpace(InstallDir))
        {
            return InstallDir;
        }

        var visualStudioRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "VisualStudio");
        if (!Directory.Exists(visualStudioRoot))
        {
            throw new DirectoryNotFoundException("Visual Studio LocalAppData root was not found.");
        }

        var candidates = Directory
            .EnumerateDirectories(visualStudioRoot, "*", SearchOption.TopDirectoryOnly)
            .SelectMany(instanceRoot =>
            {
                var extensionsDirectory = Path.Combine(instanceRoot, "Extensions");
                if (!Directory.Exists(extensionsDirectory))
                {
                    return Array.Empty<string>();
                }

                return Directory.EnumerateDirectories(extensionsDirectory, "*", SearchOption.TopDirectoryOnly);
            })
            .Where(directory =>
                File.Exists(Path.Combine(directory, "McpServer.VsExtension.McpTodo.dll")) ||
                File.Exists(Path.Combine(directory, "McpServer.VsExtension.McpTodo.pkgdef")))
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new DirectoryNotFoundException("Could not locate an installed McpTodo Visual Studio extension directory. Provide --install-dir.");
        }

        return candidates[0];
    }

    private string ResolveDevenvPath()
    {
        var envPath = Environment.GetEnvironmentVariable("DEVENV_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        var possibleVsWherePaths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "Installer", "vswhere.exe")
        };

        foreach (var vswherePath in possibleVsWherePaths.Where(File.Exists))
        {
            var result = InvokeProcess(
                vswherePath,
                new List<string> { "-latest", "-prerelease", "-products", "*", "-find", @"Common7\IDE\devenv.exe" },
                RepoRootPath,
                false);
            if (result.ExitCode == 0 && result.StandardOutputLines.Count > 0 && File.Exists(result.StandardOutputLines[0]))
            {
                return result.StandardOutputLines[0];
            }
        }

        if (CommandExists("devenv.exe"))
        {
            return "devenv.exe";
        }

        throw new FileNotFoundException("Could not locate devenv.exe. Set DEVENV_PATH or install Visual Studio.");
    }

    private string GetVsixProjectDirectory()
    {
        return Path.Combine(RepoRootPath, "src", "McpServer.VsExtension.McpTodo.Vsix");
    }

    private string GetVsixProjectPath()
    {
        return Path.Combine(GetVsixProjectDirectory(), "McpServer.VsExtension.McpTodo.Vsix.csproj");
    }

    private string ResolveVsixBuildRelativePath()
    {
        var projectDocument = XDocument.Load(GetVsixProjectPath());
        var targetFramework = projectDocument
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TargetFramework")
            ?.Value
            .Trim();

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            var targetFrameworks = projectDocument
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "TargetFrameworks")
                ?.Value;
            targetFramework = targetFrameworks?
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            throw new InvalidOperationException($"Could not determine TargetFramework from {GetVsixProjectPath()}");
        }

        var runtimeIdentifier = projectDocument
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "RuntimeIdentifier")
            ?.Value
            .Trim();

        return string.IsNullOrWhiteSpace(runtimeIdentifier)
            ? targetFramework
            : Path.Combine(targetFramework, runtimeIdentifier);
    }

    private string ResolveVsixOutputDirectory()
    {
        return Path.Combine(GetVsixProjectDirectory(), "bin", Configuration, ResolveVsixBuildRelativePath());
    }

    private string ResolveVsixObjectDirectory()
    {
        return Path.Combine(GetVsixProjectDirectory(), "obj", Configuration, ResolveVsixBuildRelativePath());
    }

    private void WriteVsixPkgDefFile(string pkgDefPath, string dllFileName)
    {
        // CreatePkgDef.exe cannot reflect the VSIX assembly after the project moved to net9.0-windows,
        // so keep emitting the same registration shape the extension already uses in Visual Studio.
        const string packageGuid = "{e8f0a1b2-3c4d-4e5f-8a9b-0c1d2e3f4a5b}";
        const string packageClass = "McpServer.VsExtension.McpTodo.McpServerMcpTodoPackage";
        const string assemblyIdentity = "McpServer.VsExtension.McpTodo, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        const string solutionExistsContextGuid = "{f1536ef8-92ec-443c-9ed7-fdadf150da82}";
        const string toolWindowGuid = "{a1b2c3d4-e5f6-7890-abcd-ef1234567890}";
        const string toolWindowClass = "McpServer.VsExtension.McpTodo.McpServerMcpTodoToolWindowPane";
        const string toolWindowHostGuid = "3ae79031-e1bc-11d0-8f78-00a0c9110057";

        var lines = new[]
        {
            $"[$RootKey$\\Packages\\{packageGuid}]",
            "@=\"McpServerMcpTodoPackage\"",
            "\"InprocServer32\"=\"$WinDir$\\SYSTEM32\\MSCOREE.DLL\"",
            $"\"Class\"=\"{packageClass}\"",
            $"\"Assembly\"=\"{assemblyIdentity}\"",
            "\"AllowsBackgroundLoad\"=dword:00000001",
            $"[$RootKey$\\AutoLoadPackages\\{solutionExistsContextGuid}]",
            $"\"{packageGuid}\"=dword:00000002",
            "[$RootKey$\\Menus]",
            $"\"{packageGuid}\"=\", Menus.ctmenu, 1\"",
            $"[$RootKey$\\ToolWindows\\{toolWindowGuid}]",
            $"@=\"{packageGuid}\"",
            $"\"Name\"=\"{toolWindowClass}\"",
            "\"Style\"=\"Tabbed\"",
            $"\"Window\"=\"{toolWindowHostGuid}\"",
            string.Empty
        };

        EnsureDirectoryExists(Path.GetDirectoryName(pkgDefPath)!);
        File.WriteAllText(pkgDefPath, string.Join(Environment.NewLine, lines), Encoding.Unicode);

        var injectCodeBasePath = Path.Combine(GetVsixProjectDirectory(), "InjectCodeBase.ps1");
        InvokeProcess(
            "pwsh",
            new List<string>
            {
                "-NoProfile",
                "-File",
                injectCodeBasePath,
                "-PkgdefPath",
                pkgDefPath,
                "-DllName",
                dllFileName
            },
            RepoRootPath,
            true);
    }

    private void RunPackageVsixTarget()
    {
        var extensionDirectory = GetVsixProjectDirectory();
        var outputDirectory = ResolveVsixOutputDirectory();
        var objectDirectory = ResolveVsixObjectDirectory();
        var stagingDirectory = Path.Combine(objectDirectory, "vsixstaging");
        var vsixPath = Path.Combine(outputDirectory, "McpServer.VsExtension.McpTodo.vsix");
        var dllPath = Path.Combine(outputDirectory, "McpServer.VsExtension.McpTodo.dll");
        var pkgDefPath = Path.Combine(objectDirectory, "McpServer.VsExtension.McpTodo.pkgdef");

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"DLL not found. Build the project first: {dllPath}");
        }

        if (!ShouldExecuteAction($"Package VSIX from {dllPath}"))
        {
            Info($"Would package VSIX to {vsixPath}");
            return;
        }

        EnsureDirectoryExists(objectDirectory);
        WriteVsixPkgDefFile(pkgDefPath, Path.GetFileName(dllPath));

        ClearDirectory(stagingDirectory);
        EnsureDirectoryExists(stagingDirectory);
        File.Copy(dllPath, Path.Combine(stagingDirectory, Path.GetFileName(dllPath)), true);
        File.Copy(pkgDefPath, Path.Combine(stagingDirectory, Path.GetFileName(pkgDefPath)), true);
        File.Copy(pkgDefPath, Path.Combine(outputDirectory, Path.GetFileName(pkgDefPath)), true);

        var manifestSource = Path.Combine(extensionDirectory, "source.extension.vsixmanifest");
        var manifestDestination = Path.Combine(stagingDirectory, "extension.vsixmanifest");
        var document = XDocument.Load(manifestSource, LoadOptions.PreserveWhitespace);
        XNamespace vsixNamespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        XNamespace designNamespace = "http://schemas.microsoft.com/developer/vsx-schema-design/2011";

        foreach (var asset in document.Descendants(vsixNamespace + "Asset"))
        {
            var type = asset.Attribute("Type")?.Value ?? string.Empty;
            if (string.Equals(type, "Microsoft.VisualStudio.VsPackage", StringComparison.Ordinal))
            {
                asset.SetAttributeValue("Path", "McpServer.VsExtension.McpTodo.pkgdef");
            }
            else if (string.Equals(type, "Microsoft.VisualStudio.MefComponent", StringComparison.Ordinal))
            {
                asset.SetAttributeValue("Path", "McpServer.VsExtension.McpTodo.dll");
            }
        }

        foreach (var element in document.Descendants())
        {
            var attributesToRemove = element.Attributes()
                .Where(attribute => attribute.Name.Namespace == designNamespace)
                .ToList();
            foreach (var attribute in attributesToRemove)
            {
                attribute.Remove();
            }
        }

        var root = document.Root;
        if (root is not null)
        {
            var namespaceAttributes = root.Attributes()
                .Where(attribute => attribute.IsNamespaceDeclaration && string.Equals(attribute.Name.LocalName, "d", StringComparison.Ordinal))
                .ToList();
            foreach (var attribute in namespaceAttributes)
            {
                attribute.Remove();
            }
        }

        var xmlWriterSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false
        };
        using (var xmlWriter = XmlWriter.Create(manifestDestination, xmlWriterSettings))
        {
            document.Save(xmlWriter);
        }

        File.WriteAllText(
            Path.Combine(stagingDirectory, "[Content_Types].xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="vsixmanifest" ContentType="text/xml"/>
              <Default Extension="dll" ContentType="application/octet-stream"/>
              <Default Extension="pkgdef" ContentType="text/plain"/>
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Override PartName="/extension.vsixmanifest" ContentType="text/xml"/>
            </Types>
            """,
            new UTF8Encoding(false));

        var relsDirectory = Path.Combine(stagingDirectory, "_rels");
        EnsureDirectoryExists(relsDirectory);
        File.WriteAllText(
            Path.Combine(relsDirectory, ".rels"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Type="http://schemas.microsoft.com/developer/vsx/2011" Target="extension.vsixmanifest" Id="manifest"/>
            </Relationships>
            """,
            new UTF8Encoding(false));

        if (File.Exists(vsixPath))
        {
            File.Delete(vsixPath);
        }

        using var archive = ZipFile.Open(vsixPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(Path.Combine(stagingDirectory, "[Content_Types].xml"), "[Content_Types].xml");
        archive.CreateEntryFromFile(Path.Combine(relsDirectory, ".rels"), "_rels/.rels");
        archive.CreateEntryFromFile(manifestDestination, "extension.vsixmanifest");
        archive.CreateEntryFromFile(Path.Combine(stagingDirectory, "McpServer.VsExtension.McpTodo.dll"), "McpServer.VsExtension.McpTodo.dll");
        archive.CreateEntryFromFile(Path.Combine(stagingDirectory, "McpServer.VsExtension.McpTodo.pkgdef"), "McpServer.VsExtension.McpTodo.pkgdef");

        Info($"VSIX package ready: {vsixPath}");
    }

    private void RunBuildAndInstallVsixTarget()
    {
        var projectPath = GetVsixProjectPath();
        if (!ShouldExecuteAction($"Build VSIX project {projectPath}"))
        {
            return;
        }

        InvokeDotNet(new List<string> { "build", projectPath, "-c", Configuration }, RepoRootPath);
        RunPackageVsixTarget();

        var vsixPath = Path.Combine(ResolveVsixOutputDirectory(), "McpServer.VsExtension.McpTodo.vsix");
        if (!SkipInstall)
        {
            if (!ShouldExecuteAction($"Launch VSIX installer for {vsixPath}"))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = vsixPath,
                WorkingDirectory = RepoRootPath,
                UseShellExecute = true
            });
        }
    }

    private void RunInstallMcpServerMcpTodoVsixTarget()
    {
        var extensionDirectory = Path.Combine(RepoRootPath, "extensions", "McpServer-mcp-todo");
        var selectedVsix = !string.IsNullOrWhiteSpace(VsixPath)
            ? ResolveLatestVsix(VsixPath)
            : Directory.GetFiles(extensionDirectory, "fwh-mcp-todo-*.vsix", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedVsix) || !File.Exists(selectedVsix))
        {
            throw new FileNotFoundException($"VSIX not found in {extensionDirectory}. Run the extension packaging flow first.");
        }

        var vsixName = Path.GetFileNameWithoutExtension(selectedVsix);
        var extractTarget = $"FunWasHad.{vsixName}";

        void InstallByExtract(string destinationDirectory)
        {
            var targetDirectory = Path.Combine(destinationDirectory, extractTarget);
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true);
            }

            EnsureDirectoryExists(targetDirectory);
            ZipFile.ExtractToDirectory(selectedVsix, targetDirectory, true);
            var innerDirectory = Path.Combine(targetDirectory, "extension");
            if (Directory.Exists(innerDirectory))
            {
                foreach (var entry in Directory.GetFileSystemEntries(innerDirectory))
                {
                    var destination = Path.Combine(targetDirectory, Path.GetFileName(entry));
                    if (Directory.Exists(entry))
                    {
                        if (Directory.Exists(destination))
                        {
                            Directory.Delete(destination, true);
                        }

                        Directory.Move(entry, destination);
                    }
                    else
                    {
                        File.Copy(entry, destination, true);
                    }
                }

                Directory.Delete(innerDirectory, true);
            }

            foreach (var packagingEntry in new[] { "[Content_Types].xml", "_rels", "extension.vsixmanifest" })
            {
                var packagingPath = Path.Combine(targetDirectory, packagingEntry);
                if (Directory.Exists(packagingPath))
                {
                    Directory.Delete(packagingPath, true);
                }
                else if (File.Exists(packagingPath))
                {
                    File.Delete(packagingPath);
                }
            }
        }

        if (!ShouldExecuteAction($"Install VSIX {selectedVsix}"))
        {
            return;
        }

        var vscodeDirectory = !string.IsNullOrWhiteSpace(ExtensionsDir) ? ExtensionsDir : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
        var cursorDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "extensions");

        void RemoveExistingExtensionDirectories(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(baseDirectory, "*fwh-mcp-todo-*", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(directory, true);
            }
        }

        if (IncludeVsCode)
        {
            if (CommandExists("code"))
            {
                InvokeProcess("code", new List<string> { "--uninstall-extension", "FunWasHad.fwh-mcp-todo" }, RepoRootPath, false);
                var installResult = InvokeProcess("code", new List<string> { "--install-extension", selectedVsix, "--force" }, RepoRootPath, false);
                if (installResult.ExitCode != 0)
                {
                    RemoveExistingExtensionDirectories(vscodeDirectory);
                    InstallByExtract(vscodeDirectory);
                }
            }
            else
            {
                RemoveExistingExtensionDirectories(vscodeDirectory);
                InstallByExtract(vscodeDirectory);
            }
        }

        if (IncludeCursor)
        {
            if (CommandExists("cursor"))
            {
                InvokeProcess("cursor", new List<string> { "--uninstall-extension", "FunWasHad.fwh-mcp-todo" }, RepoRootPath, false);
                var installResult = InvokeProcess("cursor", new List<string> { "--install-extension", selectedVsix, "--force" }, RepoRootPath, false);
                if (installResult.ExitCode != 0)
                {
                    RemoveExistingExtensionDirectories(cursorDirectory);
                    InstallByExtract(cursorDirectory);
                }
            }
            else
            {
                RemoveExistingExtensionDirectories(cursorDirectory);
                InstallByExtract(cursorDirectory);
            }
        }
    }

    private void RunDeployMcpTodoExtensionTarget()
    {
        var targetInstallDirectory = ResolveVsixInstallDirectory();
        var sourceDirectory = ResolveVsixOutputDirectory();
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"VSIX output directory not found: {sourceDirectory}");
        }

        var devenvProcesses = Process.GetProcessesByName("devenv").ToList();
        if (devenvProcesses.Count > 0 && !Force)
        {
            throw new InvalidOperationException("Visual Studio is currently running. Re-run with --force to stop it first.");
        }

        if (!ShouldExecuteAction($"Deploy Visual Studio extension files to {targetInstallDirectory}"))
        {
            return;
        }

        foreach (var devenvProcess in devenvProcesses)
        {
            try
            {
                devenvProcess.Kill(true);
            }
            catch
            {
            }
        }

        EnsureDirectoryExists(targetInstallDirectory);
        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            File.Copy(filePath, Path.Combine(targetInstallDirectory, Path.GetFileName(filePath)), true);
        }

        var pkgDefPath = Path.Combine(sourceDirectory, "McpServer.VsExtension.McpTodo.pkgdef");
        if (File.Exists(pkgDefPath))
        {
            File.Copy(pkgDefPath, Path.Combine(targetInstallDirectory, Path.GetFileName(pkgDefPath)), true);
        }

        var instanceRoot = Directory.GetParent(Directory.GetParent(targetInstallDirectory)!.FullName)!.FullName;
        var componentModelCache = Path.Combine(instanceRoot, "ComponentModelCache");
        if (Directory.Exists(componentModelCache))
        {
            Directory.Delete(componentModelCache, true);
        }

        var devenvPath = ResolveDevenvPath();
        InvokeProcess(devenvPath, new List<string> { "/updateconfiguration" }, RepoRootPath, true);
    }

    private void RunCollectAndroidCrashArtifactsTarget()
    {
        var serial = !string.IsNullOrWhiteSpace(DeviceSerial) ? DeviceSerial : "ZD222QH58Q";
        var effectiveOutputRoot = !string.IsNullOrWhiteSpace(OutputRoot)
            ? (Path.IsPathRooted(OutputRoot) ? OutputRoot : Path.Combine(RepoRootPath, OutputRoot))
            : Path.Combine(ArtifactsDirectoryPath, "android-crash", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

        string InvokeAdbCapture(IReadOnlyList<string> arguments, bool allowFailure = false)
        {
            var commandArguments = new List<string> { "-s", serial };
            commandArguments.AddRange(arguments);
            var result = InvokeProcess(ResolveAdbPath(), commandArguments, RepoRootPath, false);
            if (!allowFailure && result.ExitCode != 0)
            {
                throw new InvalidOperationException($"adb {string.Join(" ", commandArguments)} failed.{Environment.NewLine}{result.GetCombinedOutput()}");
            }

            return result.GetCombinedOutput();
        }

        void WriteArtifact(string name, string content)
        {
            var artifactPath = Path.Combine(effectiveOutputRoot, name);
            EnsureDirectoryExists(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllText(artifactPath, content ?? string.Empty, new UTF8Encoding(false));
        }

        if (!ShouldExecuteAction($"Collect Android crash artifacts into {effectiveOutputRoot}"))
        {
            return;
        }

        EnsureDirectoryExists(effectiveOutputRoot);
        InvokeProcess(ResolveAdbPath(), new List<string> { "devices" }, RepoRootPath, true);

        WriteArtifact("session-metadata.json", JsonSerializer.Serialize(new
        {
            phase = Phase,
            deviceSerial = serial,
            packageName = PackageName,
            outputRoot = effectiveOutputRoot,
            capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }, new JsonSerializerOptions { WriteIndented = true }));

        WriteArtifact("adb-devices.txt", InvokeProcess(ResolveAdbPath(), new List<string> { "devices", "-l" }, RepoRootPath, false).GetCombinedOutput());
        WriteArtifact("device-getprop.txt", InvokeAdbCapture(new List<string> { "shell", "getprop" }, true));
        WriteArtifact("device-build.txt", InvokeAdbCapture(new List<string> { "shell", "dumpsys", "package", PackageName }, true));

        if (string.Equals(Phase, "Prepare", StringComparison.OrdinalIgnoreCase))
        {
            WriteArtifact("logcat-clear.txt", InvokeAdbCapture(new List<string> { "logcat", "-b", "all", "-c" }, true));
            WriteArtifact("package-pid-before.txt", InvokeAdbCapture(new List<string> { "shell", "pidof", PackageName }, true));
            WriteArtifact(
                "README.txt",
                $"""
                 Prepared Android crash capture workspace at:
                 {effectiveOutputRoot}

                 Next steps:
                 1. Reproduce the crash on device {serial}.
                 2. Relaunch the app once after the crash so recovered crash diagnostics can be replayed into the app log.
                 3. Run:
                    dotnet run --project build/Build.csproj -- --target CollectAndroidCrashArtifacts --phase Collect --device-serial {serial} --output-root "{effectiveOutputRoot}"

                 Optional:
                 - Add --include-bugreport if you suspect a native crash or ANR and need a full system bugreport.
                 """);
            return;
        }

        WriteArtifact("logcat.txt", InvokeAdbCapture(new List<string> { "logcat", "-d", "-b", "all", "-v", "threadtime" }, true));
        WriteArtifact("package-pid-after.txt", InvokeAdbCapture(new List<string> { "shell", "pidof", PackageName }, true));
        WriteArtifact("meminfo.txt", InvokeAdbCapture(new List<string> { "shell", "dumpsys", "meminfo", PackageName }, true));
        WriteArtifact("activity-exit-info.txt", InvokeAdbCapture(new List<string> { "shell", "dumpsys", "activity", "exit-info", PackageName }, true));
        WriteArtifact("tombstones-access.txt", InvokeAdbCapture(new List<string> { "shell", "ls", "-al", "/data/tombstones" }, true));

        var listing = InvokeAdbCapture(new List<string> { "shell", "run-as", PackageName, "sh", "-c", "ls -1 files/diagnostics/crash 2>/dev/null" }, true);
        WriteArtifact("app-diagnostics-listing.txt", listing);
        foreach (var name in SplitLines(listing))
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                var content = InvokeProcess(
                    "adb",
                    new List<string> { "-s", serial, "exec-out", "run-as", PackageName, "cat", $"files/diagnostics/crash/{trimmed}" },
                    RepoRootPath,
                    false).GetCombinedOutput();
                WriteArtifact(Path.Combine("app-diagnostics", trimmed.Replace("/", "_", StringComparison.Ordinal)), content);
            }
            else
            {
                WriteArtifact(Path.Combine("app-diagnostics", trimmed.Replace("/", "_", StringComparison.Ordinal) + ".note.txt"), $"Skipped non-text artifact '{trimmed}'.");
            }
        }

        if (IncludeBugreport)
        {
            var bugreportBase = Path.Combine(effectiveOutputRoot, "bugreport");
            var bugreportResult = InvokeProcess(ResolveAdbPath(), new List<string> { "-s", serial, "bugreport", bugreportBase }, RepoRootPath, false);
            WriteArtifact("bugreport-command-output.txt", bugreportResult.GetCombinedOutput());
        }
    }

    private void RunStartWebUiTarget()
    {
        var projectPath = Path.Combine(RepoRootPath, "src", "McpServer.Web", "McpServer.Web.csproj");
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"McpServer.Web project not found at '{projectPath}'.");
        }

        if (KillExisting && OperatingSystem.IsWindows())
        {
            foreach (var processInfo in QueryWebProcesses())
            {
                try
                {
                    Process.GetProcessById(processInfo.ProcessId).Kill(true);
                }
                catch
                {
                }
            }
        }

        if (!NoBuild)
        {
            if (!ShouldExecuteAction($"Build McpServer.Web ({Configuration})"))
            {
                return;
            }

            InvokeDotNet(new List<string> { "build", projectPath, "-c", Configuration }, RepoRootPath);
        }

        var logRoot = Path.Combine(LogsDirectoryPath, "web-ui-startup");
        EnsureDirectoryExists(logRoot);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var outLog = Path.Combine(logRoot, $"web-ui-{stamp}.out.log");
        var errLog = Path.Combine(logRoot, $"web-ui-{stamp}.err.log");
        var diagLog = Path.Combine(logRoot, $"web-ui-{stamp}.diag.json");
        var baseUrl = $"http://localhost:{Port}";

        if (!ShouldExecuteAction($"Start McpServer.Web on {baseUrl}"))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = RepoRootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "run", "--project", projectPath, "-c", Configuration, "--no-build", "--urls", baseUrl })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        var started = false;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                break;
            }

            try
            {
                using var response = httpClient.GetAsync(baseUrl).GetAwaiter().GetResult();
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 500)
                {
                    started = true;
                    break;
                }
            }
            catch
            {
            }

            Thread.Sleep(500);
        }

        if (!started)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            Task.WaitAll(stdoutTask, stderrTask);
            File.WriteAllText(outLog, stdoutTask.Result, new UTF8Encoding(false));
            File.WriteAllText(errLog, stderrTask.Result, new UTF8Encoding(false));
            File.WriteAllText(
                diagLog,
                JsonSerializer.Serialize(new
                {
                    timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    baseUrl,
                    timeoutSeconds = TimeoutSeconds,
                    processId = process.Id,
                    processSnapshot = QueryWebProcesses(),
                    stdoutTail = TailLines(outLog, 60),
                    stderrTail = TailLines(errLog, 60)
                }, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            throw new InvalidOperationException($"Startup timed out or exited early. Diagnostics: {diagLog}");
        }

        Task.Run(async () =>
        {
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            File.WriteAllText(outLog, stdout, new UTF8Encoding(false));
            File.WriteAllText(errLog, stderr, new UTF8Encoding(false));
        });

        WriteJsonToConsole(new
        {
            success = true,
            url = baseUrl,
            processId = process.Id,
            outLog,
            errLog,
            diagLog
        });
    }

    private void RunCheckPackageVersionsTarget()
    {
        var packageVersions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in Directory.EnumerateFiles(Path.Combine(RepoRootPath, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(projectPath);
            foreach (var packageReference in document.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
            {
                var include = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("Update")?.Value ?? string.Empty;
                var version = packageReference.Attribute("Version")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(include) || string.IsNullOrWhiteSpace(version) || version.Contains("$", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!packageVersions.TryGetValue(include, out var versions))
                {
                    versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    packageVersions[include] = versions;
                }

                versions.Add(version);
            }
        }

        Info("Packages needing consolidation:");
        var foundIssues = false;
        foreach (var pair in packageVersions.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            foundIssues = true;
            Info($"{pair.Key}: {string.Join(", ", pair.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");
        }

        if (!foundIssues)
        {
            Info("No package version discrepancies found.");
        }
    }

    private void RunLaunchVsCodeExtensionTarget()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceFolder) || string.IsNullOrWhiteSpace(ExtensionDevelopmentPath))
        {
            throw new InvalidOperationException("WorkspaceFolder and ExtensionDevelopmentPath are required.");
        }

        var codeExecutable = !string.IsNullOrWhiteSpace(VsCodePath)
            ? VsCodePath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
        if (!File.Exists(codeExecutable))
        {
            throw new FileNotFoundException($"VS Code executable not found at '{codeExecutable}'.");
        }

        if (!ShouldExecuteAction($"Launch VS Code with extension path {ExtensionDevelopmentPath}"))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = codeExecutable,
            WorkingDirectory = RepoRootPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add($"--extensionDevelopmentPath={ExtensionDevelopmentPath}");
        startInfo.ArgumentList.Add(WorkspaceFolder);
        Process.Start(startInfo);
    }

    private void RunListVsixTarget()
    {
        var vsixFilePath = ResolveLatestVsix(VsixPath);
        using var archive = ZipFile.OpenRead(vsixFilePath);
        foreach (var entry in archive.Entries.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{entry.FullName} ({entry.Length} bytes)");
        }
    }

    private void RunReadVsixTarget()
    {
        var vsixFilePath = ResolveLatestVsix(VsixPath);
        if (string.IsNullOrWhiteSpace(VsixEntry))
        {
            throw new InvalidOperationException("VsixEntry is required.");
        }

        using var archive = ZipFile.OpenRead(vsixFilePath);
        var entry = archive.GetEntry(VsixEntry);
        if (entry is null)
        {
            throw new FileNotFoundException($"Entry '{VsixEntry}' was not found in '{vsixFilePath}'.");
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Console.WriteLine(reader.ReadToEnd());
    }

    private void RunGenerateFdroidRepoTarget()
    {
        var workspaceDirectory = Path.Combine(RepoRootPath, "fdroid-workspace");
        var repoDirectory = Path.Combine(workspaceDirectory, "repo");
        var metadataDirectory = Path.Combine(workspaceDirectory, "metadata");
        var configPath = Path.Combine(workspaceDirectory, "config.yml");
        var keystorePath = Path.Combine(workspaceDirectory, "keystore.jks");
        var repoUrl = ResolveDefaultFdroidRepoUrl();

        if (!ShouldExecuteAction($"Generate F-Droid repo in {workspaceDirectory}"))
        {
            return;
        }

        EnsureDirectoryExists(repoDirectory);
        EnsureDirectoryExists(metadataDirectory);
        EnsureDirectoryExists(Path.Combine(repoDirectory, "icons"));

        if (!File.Exists(keystorePath))
        {
            InvokeProcess(
                "keytool",
                new List<string>
                {
                    "-genkeypair",
                    "-keystore",
                    keystorePath,
                    "-alias",
                    "fdroidrepo",
                    "-keyalg",
                    "RSA",
                    "-keysize",
                    "2048",
                    "-validity",
                    "10000",
                    "-storepass",
                    "android",
                    "-keypass",
                    "android",
                    "-dname",
                    "CN=Request Tracker Repo, OU=CI, O=GitHub"
                },
                RepoRootPath,
                true);
        }

        File.WriteAllText(
            configPath,
            $"""
             repo_url: "{repoUrl}"
             repo_name: "Request Tracker"
             repo_description: "Android app for browsing and analyzing Copilot session logs. Supports phone and tablet layouts."
             repo_keyalias: fdroidrepo
             keystore: keystore.jks
             keystorepass: android
             keypass: android
             keydname: "CN=Request Tracker Repo, OU=CI, O=GitHub"
             """,
            new UTF8Encoding(false));

        var iconSource = Path.Combine(RepoRootPath, "docs", "fdroid", "icon.png");
        var iconDestination = Path.Combine(repoDirectory, "icons", "icon.png");
        if (File.Exists(iconSource))
        {
            File.Copy(iconSource, iconDestination, true);
        }
        else
        {
            File.WriteAllBytes(iconDestination, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        }

        foreach (var staleFile in Directory.EnumerateFiles(repoDirectory, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(staleFile);
        }

        foreach (var staleIndex in Directory.EnumerateFiles(repoDirectory, "index.*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(staleIndex);
        }

        var apkPath = Directory.EnumerateFiles(RepoRootPath, "*.apk", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apkPath))
        {
            throw new FileNotFoundException("No APK found for F-Droid repo generation.");
        }

        File.Copy(apkPath, Path.Combine(repoDirectory, Path.GetFileName(apkPath)), true);
        var metadataSource = Path.Combine(RepoRootPath, "fdroid", "metadata", "ninja.thesharp.mcpservermanager.yml");
        File.Copy(metadataSource, Path.Combine(metadataDirectory, Path.GetFileName(metadataSource)), true);

        InvokeProcess("fdroid", new List<string> { "update" }, workspaceDirectory, true);
    }

    private void RunVerifyFdroidRepoTarget()
    {
        var workspaceDirectory = Path.Combine(RepoRootPath, "fdroid-workspace");
        var repoDirectory = Path.Combine(workspaceDirectory, "repo");
        var iconPath = Path.Combine(repoDirectory, "icons", "icon.png");
        var configPath = Path.Combine(workspaceDirectory, "config.yml");

        if (!File.Exists(iconPath) || new FileInfo(iconPath).Length == 0)
        {
            throw new InvalidOperationException($"F-Droid icon file missing or empty at {iconPath}");
        }

        var apkPath = Directory.EnumerateFiles(repoDirectory, "*.apk", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apkPath))
        {
            throw new InvalidOperationException($"No APK found in {repoDirectory}");
        }

        var expectedRepoUrl = ResolveDefaultFdroidRepoUrl();
        var configUrl = File.ReadAllLines(configPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("repo_url:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(line.IndexOf(':') + 1)..].Trim().Trim('"'))
            .FirstOrDefault() ?? string.Empty;
        if (!string.Equals(configUrl, expectedRepoUrl, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"F-Droid repo_url mismatch: '{configUrl}' != '{expectedRepoUrl}'");
        }

        var apkName = Path.GetFileName(apkPath);
        var computedHash = ComputeSha256(apkPath);
        var indexV1Path = Path.Combine(repoDirectory, "index-v1.json");
        var indexXmlPath = Path.Combine(repoDirectory, "index.xml");
        var indexHash = string.Empty;
        var versionMatch = string.Empty;

        if (File.Exists(indexV1Path))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(indexV1Path));
            var packages = document.RootElement.TryGetProperty("packages", out var packagesElement)
                ? packagesElement
                : default;
            if (packages.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("index-v1.json does not contain a packages object.");
            }

            foreach (var package in packages.EnumerateObject())
            {
                foreach (var release in package.Value.EnumerateArray())
                {
                    if ((release.TryGetProperty("apkName", out var apkNameProperty) ? apkNameProperty.GetString() : string.Empty) != apkName)
                    {
                        continue;
                    }

                    indexHash = (release.TryGetProperty("hash", out var hashProperty) ? hashProperty.GetString() : string.Empty) ?? string.Empty;
                    versionMatch = (release.TryGetProperty("versionName", out var versionProperty) ? versionProperty.GetString() : string.Empty) ?? string.Empty;
                    indexHash = indexHash.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                    break;
                }
            }
        }
        else if (File.Exists(indexXmlPath))
        {
            var document = XDocument.Load(indexXmlPath);
            var packageElement = document.Descendants("package")
                .FirstOrDefault(x => string.Equals(x.Attribute("apkName")?.Value, apkName, StringComparison.Ordinal));
            if (packageElement is not null)
            {
                indexHash = packageElement.Attribute("hash")?.Value ?? string.Empty;
                versionMatch = packageElement.Attribute("versionName")?.Value ?? string.Empty;
                indexHash = indexHash.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(indexHash))
        {
            throw new InvalidOperationException($"APK '{apkName}' was not found in the F-Droid index.");
        }

        if (!string.Equals(indexHash, computedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Hash mismatch: index '{indexHash}' != computed '{computedHash}'.");
        }

        if (!string.IsNullOrWhiteSpace(ExpectedVersion) &&
            !string.IsNullOrWhiteSpace(versionMatch) &&
            !string.Equals(ExpectedVersion, versionMatch, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected version '{ExpectedVersion}' but F-Droid index references '{versionMatch}'.");
        }
    }

    private void RunPreparePagesArtifactTarget()
    {
        var pagesOutputDirectory = Path.Combine(RepoRootPath, "pages-output");
        var pagesRepoDirectory = Path.Combine(pagesOutputDirectory, "repo");
        var fdroidWorkspaceRepoDirectory = Path.Combine(RepoRootPath, "fdroid-workspace", "repo");

        if (!ShouldExecuteAction($"Prepare pages artifact in {pagesOutputDirectory}"))
        {
            return;
        }

        ClearDirectory(pagesOutputDirectory);
        EnsureDirectoryExists(pagesRepoDirectory);

        var fdroidDocsDirectory = Path.Combine(RepoRootPath, "docs", "fdroid");
        File.Copy(Path.Combine(fdroidDocsDirectory, "index.html"), Path.Combine(pagesOutputDirectory, "index.html"), true);

        var noJekyllPath = Path.Combine(fdroidDocsDirectory, ".nojekyll");
        if (File.Exists(noJekyllPath))
        {
            File.Copy(noJekyllPath, Path.Combine(pagesOutputDirectory, ".nojekyll"), true);
        }
        else
        {
            File.WriteAllText(Path.Combine(pagesOutputDirectory, ".nojekyll"), string.Empty, new UTF8Encoding(false));
        }

        var iconSource = Path.Combine(fdroidDocsDirectory, "icon.png");
        if (File.Exists(iconSource))
        {
            File.Copy(iconSource, Path.Combine(pagesOutputDirectory, "icon.png"), true);
        }

        CopyDirectoryContents(fdroidWorkspaceRepoDirectory, pagesRepoDirectory);
    }
}
