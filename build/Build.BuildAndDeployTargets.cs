using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Nuke.Common.Logger;

partial class Build
{
    private DeploymentResult CreateDeploymentResult(string target, string status, string message)
    {
        return new DeploymentResult
        {
            Target = target,
            Status = status,
            Message = message
        };
    }

    private bool ShouldExecuteAction(string description)
    {
        if (!WhatIf)
        {
            return true;
        }

        Warn($"[WhatIf] {description}");
        return false;
    }

    private string PowerShellBool(bool value)
    {
        return value ? "$true" : "$false";
    }

    private List<string> ParseDeploySelections()
    {
        var selections = new List<string>();
        if (string.IsNullOrWhiteSpace(DeploySelection))
        {
            selections.Add("All");
        }
        else
        {
            foreach (var segment in Regex.Split(DeploySelection, @"[\s,;]+"))
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                selections.Add(segment.Trim());
            }
        }

        if (selections.Any(x => string.Equals(x, "All", StringComparison.OrdinalIgnoreCase)))
        {
            return new List<string>
            {
                "Director",
                "WebUi",
                "AndroidPhone",
                "AndroidEmulator",
                "DesktopMsix",
                "DesktopDeb"
            };
        }

        return selections;
    }

    private void ShowDeploySummary(List<DeploymentResult> results)
    {
        Info("Deployment summary");
        foreach (var result in results)
        {
            Info($"{result.Target}: {result.Status} - {result.Message}");
        }

        var successCount = results.Count(x => x.Status == "Success");
        var whatIfCount = results.Count(x => x.Status == "WhatIf");
        var skippedCount = results.Count(x => x.Status == "Skipped");
        var failedCount = results.Count(x => x.Status == "Failed");
        Info($"Success={successCount}  WhatIf={whatIfCount}  Skipped={skippedCount}  Failed={failedCount}");
    }

    private string ExecuteDotnetToolPipeline(
        string projectPath,
        string toolId,
        string toolCommand,
        string nupkgDirectory,
        bool installAfterPack,
        bool skipVersionBumpValue,
        bool skipProcessStopValue)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project not found: {projectPath}");
        }

        if (!skipVersionBumpValue)
        {
            RunBumpGitVersionPatch();
        }

        var version = ResolveVersionDetails(PackageVersion);
        if (!skipProcessStopValue && !string.IsNullOrWhiteSpace(toolCommand) && !WhatIf)
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!string.Equals(process.ProcessName, toolCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill(true);
                }
                catch
                {
                }
            }
        }

        var outputDirectory = Path.IsPathRooted(nupkgDirectory) ? nupkgDirectory : Path.Combine(RepoRootPath, nupkgDirectory);
        EnsureDirectoryExists(outputDirectory);

        var targetFramework = ResolveTargetFramework(projectPath, "net9.0");
        var publishDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", Configuration, targetFramework, "publish");
        var projectDocument = XDocument.Load(projectPath);
        var propertyGroups = projectDocument.Root?.Elements().Where(x => x.Name.LocalName == "PropertyGroup").ToList() ?? new List<XElement>();
        var description = toolId;
        var authors = "SharpNinja";
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var toolCommandName = toolCommand;

        foreach (var propertyGroup in propertyGroups)
        {
            foreach (var element in propertyGroup.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Description" when !string.IsNullOrWhiteSpace(element.Value):
                        description = element.Value.Trim();
                        break;
                    case "Authors" when !string.IsNullOrWhiteSpace(element.Value):
                        authors = element.Value.Trim();
                        break;
                    case "AssemblyName" when !string.IsNullOrWhiteSpace(element.Value):
                        assemblyName = element.Value.Trim();
                        break;
                    case "ToolCommandName" when !string.IsNullOrWhiteSpace(element.Value):
                        toolCommandName = element.Value.Trim();
                        break;
                }
            }
        }

        var dotnetToolSettingsPath = Path.Combine(publishDirectory, "DotnetToolSettings.xml");
        var nuspecPath = Path.Combine(outputDirectory, $"{toolId}.nuspec");
        var nupkgPath = Path.Combine(outputDirectory, $"{toolId}.{version.SemVer}.nupkg");

        if (!ShouldExecuteAction($"Publish and pack {toolId} ({Configuration})"))
        {
            return nupkgPath;
        }

        InvokeDotNet(new List<string> { "tool", "uninstall", "--global", toolId }, RepoRootPath, false);

        EnsureDirectoryExists(publishDirectory);
        InvokeDotNet(
            new List<string>
            {
                "publish",
                projectPath,
                "-c",
                Configuration,
                "-o",
                publishDirectory
            },
            RepoRootPath);

        File.WriteAllText(
            dotnetToolSettingsPath,
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <DotNetCliTool Version="1">
               <Commands>
                 <Command Name="{toolCommandName}" EntryPoint="{assemblyName}.dll" Runner="dotnet" />
               </Commands>
             </DotNetCliTool>
             """,
            new UTF8Encoding(false));

        File.WriteAllText(
            nuspecPath,
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
               <metadata>
                 <id>{toolId}</id>
                 <version>{version.SemVer}</version>
                 <authors>{authors}</authors>
                 <description>{description}</description>
                 <packageTypes>
                   <packageType name="DotnetTool" />
                 </packageTypes>
               </metadata>
               <files>
                 <file src="{publishDirectory}\**" target="tools/{targetFramework}/any" />
               </files>
             </package>
             """,
            new UTF8Encoding(false));

        if (File.Exists(nupkgPath))
        {
            File.Delete(nupkgPath);
        }

        var supportsDotNetToolPack = propertyGroups
            .SelectMany(group => group.Elements())
            .Any(element =>
                string.Equals(element.Name.LocalName, "PackAsTool", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(element.Value.Trim(), out var enabled) &&
                enabled);

        if (CommandExists("nuget"))
        {
            InvokeProcess("nuget", new List<string> { "pack", nuspecPath, "-OutputDirectory", outputDirectory, "-NoPackageAnalysis" }, RepoRootPath, true);
        }
        else if (supportsDotNetToolPack)
        {
            InvokeDotNet(
                new List<string>
                {
                    "pack",
                    projectPath,
                    "-c",
                    Configuration,
                    "-o",
                    outputDirectory,
                    "/p:PackageVersion=" + version.SemVer,
                    "/p:Version=" + version.SemVer,
                    "/p:AssemblyVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
                    "/p:FileVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
                    "/p:InformationalVersion=" + version.SemVer
                },
                RepoRootPath,
                true);
        }
        else
        {
            throw new InvalidOperationException($"nuget.exe was not found in PATH and {Path.GetFileName(projectPath)} does not declare PackAsTool for dotnet pack fallback.");
        }

        if (installAfterPack)
        {
            InvokeDotNet(
                new List<string>
                {
                    "tool",
                    "install",
                    "--global",
                    toolId,
                    "--add-source",
                    outputDirectory,
                    "--version",
                    version.SemVer
                },
                RepoRootPath);
        }

        return nupkgPath;
    }

    private string BuildAndroidPackageCore()
    {
        var projectPath = Path.Combine(RepoRootPath, "src", "McpServerManager.Android", "McpServerManager.Android.csproj");
        var targetFramework = ResolveTargetFramework(projectPath, "net9.0-android");
        var version = ResolveVersionDetails(PackageVersion);
        var artifactsDirectory = ArtifactsDirectoryPath;
        EnsureDirectoryExists(artifactsDirectory);
        ApplyMarkdownAvaloniaLinuxPatchIfNeeded();

        var signingArguments = new List<string>();
        var keystoreBase64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");
        if (!string.IsNullOrWhiteSpace(keystoreBase64))
        {
            var signingDirectory = Path.Combine(artifactsDirectory, "android-signing");
            EnsureDirectoryExists(signingDirectory);
            var keystorePath = Path.Combine(signingDirectory, "release.keystore");
            File.WriteAllBytes(keystorePath, Convert.FromBase64String(keystoreBase64));

            var keyAlias = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS") ?? string.Empty;
            var storePassword = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASSWORD") ?? string.Empty;
            var keyPassword = Environment.GetEnvironmentVariable("ANDROID_KEY_PASSWORD") ?? string.Empty;
            signingArguments.Add($"/p:AndroidSigningKeyStore={keystorePath}");
            signingArguments.Add($"/p:AndroidSigningKeyAlias={keyAlias}");
            signingArguments.Add($"/p:AndroidSigningStorePass={storePassword}");
            signingArguments.Add($"/p:AndroidSigningKeyPass={keyPassword}");
        }

        var destinationApk = Path.Combine(artifactsDirectory, $"McpServerManager-{version.SemVer}.apk");
        if (!ShouldExecuteAction($"Build Android APK {version.SemVer}"))
        {
            return destinationApk;
        }

        var publishArguments = new List<string>
        {
            "publish",
            projectPath,
            "/p:Version=" + version.SemVer,
            "/p:AssemblyVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
            "/p:FileVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
            "/p:InformationalVersion=" + version.SemVer,
            "-c",
            Configuration,
            "-f",
            targetFramework,
            "/p:ApplicationVersion=" + version.VersionCode.ToString(CultureInfo.InvariantCulture),
            "/p:ApplicationDisplayVersion=" + version.SemVer
        };
        publishArguments.AddRange(signingArguments);

        InvokeDotNet(publishArguments, RepoRootPath);

        var releaseDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", Configuration);
        var apkCandidates = Directory.GetFiles(releaseDirectory, "*-Signed.apk", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(releaseDirectory, "*.apk", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        if (apkCandidates.Count == 0)
        {
            throw new FileNotFoundException("No Android APK was found after publish.");
        }

        File.Copy(apkCandidates[0], destinationApk, true);
        WriteJsonToConsole(new
        {
            path = destinationApk,
            sha256 = ComputeSha256(destinationApk),
            size = new FileInfo(destinationApk).Length
        });

        return destinationApk;
    }

    private void DeployAndroidCore(string deviceSerial)
    {
        var projectPath = Path.Combine(RepoRootPath, "src", "McpServerManager.Android", "McpServerManager.Android.csproj");
        var targetFramework = ResolveTargetFramework(projectPath, "net9.0-android");

        if (!ShouldExecuteAction($"Deploy Android app to {deviceSerial}"))
        {
            return;
        }

        var devices = InvokeProcess(ResolveAdbPath(), new List<string> { "devices", "-l" }, RepoRootPath, true);
        foreach (var line in devices.StandardOutputLines)
        {
            Info(line);
        }

        InvokeDotNet(
            new List<string>
            {
                "build",
                projectPath,
                "-t:Install",
                "-f",
                targetFramework,
                "-c",
                Configuration,
                $"-p:AdbTarget=-s {deviceSerial}"
            },
            RepoRootPath);
    }

    private string BuildDesktopMsixCore(bool installAfterBuild)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("MSIX packaging is only available on Windows.");
        }

        if (installAfterBuild && !IsWindowsAdministrator())
        {
            return BuildDesktopMsixWithElevation();
        }

        return BuildDesktopMsixCoreNative(installAfterBuild);
    }

    private string BuildDesktopDebCore(bool installAfterBuild)
    {
        var version = ResolveVersionDetails(PackageVersion);
        var outputDirectory = ResolveOutputDirectory();
        var runtimeId = ResolveDesktopDebRid();
        EnsureDirectoryExists(outputDirectory);

        var projectPath = Path.Combine(RepoRootPath, "src", "McpServerManager.Desktop", "McpServerManager.Desktop.csproj");
        var publishDirectory = Path.Combine(outputDirectory, "publish-linux");
        var debStagingDirectory = Path.Combine(outputDirectory, "deb-staging");
        var debFilePath = Path.Combine(outputDirectory, $"mcpservermanager_{version.SemVer}_amd64.deb");
        var applicationDirectory = Path.Combine(debStagingDirectory, "opt", "mcpservermanager");

        if (!ShouldExecuteAction($"Build desktop DEB {version.SemVer}"))
        {
            return debFilePath;
        }

        ApplyMarkdownAvaloniaLinuxPatchIfNeeded();

        if (!NoBuild)
        {
            EnsureDirectoryExists(publishDirectory);
            InvokeDotNet(
                new List<string>
                {
                    "publish",
                    projectPath,
                    "-c",
                    Configuration,
                    "-r",
                    runtimeId,
                    "-f",
                    "net9.0",
                    "--self-contained",
                    "true",
                    "-p:PublishSingleFile=true",
                    "-p:IncludeNativeLibrariesForSelfExtract=true",
                    "-o",
                    publishDirectory
                },
                RepoRootPath);
        }

        if (!Directory.Exists(publishDirectory))
        {
            throw new DirectoryNotFoundException($"Publish output not found at {publishDirectory}");
        }

        ClearDirectory(debStagingDirectory);
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "DEBIAN"));
        EnsureDirectoryExists(applicationDirectory);
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "usr", "bin"));
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "usr", "share", "applications"));
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "128x128", "apps"));
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "256x256", "apps"));
        EnsureDirectoryExists(Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "512x512", "apps"));

        CopyDirectoryContents(publishDirectory, applicationDirectory);

        var executableName = "McpServerManager.Desktop";
        var expectedExecutablePath = Path.Combine(applicationDirectory, executableName);
        if (!File.Exists(expectedExecutablePath))
        {
            var firstExecutable = Directory.GetFiles(applicationDirectory)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstExecutable))
            {
                throw new FileNotFoundException("Could not determine the desktop executable in publish output.");
            }

            executableName = Path.GetFileName(firstExecutable);
        }

        File.WriteAllText(
            Path.Combine(debStagingDirectory, "usr", "share", "applications", "mcpservermanager.desktop"),
            $"""
             [Desktop Entry]
             Name=McpServerManager
             Comment=Browse and analyze Copilot request/session logs
             Exec=/opt/mcpservermanager/{executableName}
             Icon=mcpservermanager
             Terminal=false
             Type=Application
             Categories=Development;Utility;
             StartupWMClass=McpServerManager.Desktop
             """,
            new UTF8Encoding(false));

        var controlFilePath = Path.Combine(debStagingDirectory, "DEBIAN", "control");
        var installedSizeKb = Directory.GetFiles(applicationDirectory, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path).Length)
            .Sum(length => length) / 1024;
        File.WriteAllText(
            controlFilePath,
            string.Join(
                "\n",
                new[]
                {
                    "Package: mcpservermanager",
                    $"Version: {version.SemVer}",
                    "Section: devel",
                    "Priority: optional",
                    "Architecture: amd64",
                    $"Installed-Size: {installedSizeKb}",
                    "Maintainer: sharpninja <ninja@thesharp.ninja>",
                    "Description: McpServerManager Desktop",
                    " Avalonia desktop app for browsing, searching, and analyzing",
                    " Copilot request/session logs. Supports portrait and landscape",
                    " layouts with tree view, markdown/JSON viewer, and search history.",
                    "Homepage: https://github.com/sharpninja/McpServerManager",
                    string.Empty
                }),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(debStagingDirectory, "DEBIAN", "postinst"),
            "#!/bin/sh\nset -e\nupdate-desktop-database /usr/share/applications 2>/dev/null || true\ngtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(debStagingDirectory, "DEBIAN", "postrm"),
            "#!/bin/sh\nset -e\nupdate-desktop-database /usr/share/applications 2>/dev/null || true\ngtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true\n",
            new UTF8Encoding(false));

        var iconDirectory = Path.Combine(RepoRootPath, "src", "McpServerManager.Core", "Assets");
        var iconTargets = new Dictionary<string, string>
        {
            [Path.Combine(iconDirectory, "logo-128.png")] = Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "128x128", "apps", "mcpservermanager.png"),
            [Path.Combine(iconDirectory, "logo-256.png")] = Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "256x256", "apps", "mcpservermanager.png"),
            [Path.Combine(iconDirectory, "logo-512.png")] = Path.Combine(debStagingDirectory, "usr", "share", "icons", "hicolor", "512x512", "apps", "mcpservermanager.png")
        };

        foreach (var pair in iconTargets)
        {
            if (File.Exists(pair.Key))
            {
                File.Copy(pair.Key, pair.Value, true);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var distro = ResolveWslDistroName(WslDistro);
            var wslDebRootSource = ConvertToWslPath(distro, debStagingDirectory);
            var wslDebPath = ConvertToWslPath(distro, debFilePath);
            var wslBuildRoot = $"/tmp/mcpservermanager-deb-{Guid.NewGuid():N}";
            var wslDebRoot = $"{wslBuildRoot}/package";
            var wslTempDebPath = $"{wslBuildRoot}/{Path.GetFileName(debFilePath)}";
            var wslDebRootSourceContents = $"{wslDebRootSource}/.";
            var wslControlDirectory = $"{wslDebRoot}/DEBIAN";
            var wslControlPath = $"{wslControlDirectory}/control";
            var wslAppExecutablePath = $"{wslDebRoot}/opt/mcpservermanager/{executableName}";
            var wslPostinstPath = $"{wslControlDirectory}/postinst";
            var wslPostrmPath = $"{wslControlDirectory}/postrm";

            // Build the Debian package inside the Linux filesystem so dpkg-deb sees valid Unix permissions.
            InvokeWslCommand(distro, $"rm -rf {QuoteBashLiteral(wslBuildRoot)} && mkdir -p {QuoteBashLiteral(wslDebRoot)} && cp -a {QuoteBashLiteral(wslDebRootSourceContents)} {QuoteBashLiteral(wslDebRoot)}/");
            InvokeWslCommand(distro, $"chmod 0755 {QuoteBashLiteral(wslControlDirectory)} && chmod 0644 {QuoteBashLiteral(wslControlPath)} && chmod 0755 {QuoteBashLiteral(wslPostinstPath)} {QuoteBashLiteral(wslPostrmPath)} {QuoteBashLiteral(wslAppExecutablePath)}");
            InvokeWslCommand(distro, $"dpkg-deb --build --root-owner-group {QuoteBashLiteral(wslDebRoot)} {QuoteBashLiteral(wslTempDebPath)}");
            InvokeWslCommand(distro, $"cp {QuoteBashLiteral(wslTempDebPath)} {QuoteBashLiteral(wslDebPath)}");
            if (installAfterBuild)
            {
                Info($"Launching interactive WSL sudo in distro '{distro}'. Enter your password if prompted.");
                InvokeInteractiveWslCommand(distro, $"sudo dpkg -i {QuoteBashLiteral(wslTempDebPath)}");
            }

            InvokeWslCommand(distro, $"rm -rf {QuoteBashLiteral(wslBuildRoot)}");
        }
        else
        {
            InvokeProcess("chmod", new List<string> { "+x", Path.Combine(applicationDirectory, executableName), Path.Combine(debStagingDirectory, "DEBIAN", "postinst"), Path.Combine(debStagingDirectory, "DEBIAN", "postrm") }, RepoRootPath, true);
            InvokeProcess("dpkg-deb", new List<string> { "--build", "--root-owner-group", debStagingDirectory, debFilePath }, RepoRootPath, true);
            if (installAfterBuild)
            {
                Info("Launching interactive sudo for desktop DEB install. Enter your password if prompted.");
                InvokeInteractiveProcess("sudo", new List<string> { "dpkg", "-i", debFilePath }, RepoRootPath, true);
            }
        }

        return debFilePath;
    }

    private string ResolveDesktopDebRid()
    {
        var runtimeId =
            string.IsNullOrWhiteSpace(Rid) ||
            string.Equals(Rid, "win-x64", StringComparison.OrdinalIgnoreCase)
                ? "linux-x64"
                : Rid;

        if (!runtimeId.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Desktop DEB packaging requires a Linux RID, but '{runtimeId}' was provided.");
        }

        return runtimeId;
    }

    private DeploymentResult DeployDirectorCore()
    {
        try
        {
            if (!CommandExists("dotnet"))
            {
                return CreateDeploymentResult("Director", "Skipped", "dotnet was not found in PATH.");
            }

            if (!CommandExists("nuget") && !CommandExists("dotnet"))
            {
                return CreateDeploymentResult("Director", "Skipped", "nuget or dotnet nuget was not found in PATH.");
            }

            var nupkgPath = ExecuteDotnetToolPipeline(
                Path.Combine(RepoRootPath, "src", "McpServer.Director", "McpServer.Director.csproj"),
                "SharpNinja.McpServer.Director",
                "director",
                "nupkg",
                installAfterPack: true,
                skipVersionBumpValue: true,
                skipProcessStopValue: SkipProcessStop);

            return CreateDeploymentResult("Director", WhatIf ? "WhatIf" : "Success", WhatIf ? $"Would install Director from {nupkgPath}." : $"Installed Director from {nupkgPath}.");
        }
        catch (Exception ex)
        {
            return CreateDeploymentResult("Director", "Failed", ex.Message);
        }
    }

    private DeploymentResult DeployWebUiCore()
    {
        try
        {
            if (!CommandExists("dotnet"))
            {
                return CreateDeploymentResult("WebUi", "Skipped", "dotnet was not found in PATH.");
            }

            if (!CommandExists("nuget") && !CommandExists("dotnet"))
            {
                return CreateDeploymentResult("WebUi", "Skipped", "nuget or dotnet nuget was not found in PATH.");
            }

            var nupkgPath = ExecuteDotnetToolPipeline(
                Path.Combine(RepoRootPath, "src", "McpServer.Web", "McpServer.Web.csproj"),
                "SharpNinja.McpServer.Web",
                "mcp-web",
                "nupkg",
                installAfterPack: true,
                skipVersionBumpValue: true,
                skipProcessStopValue: true);

            return CreateDeploymentResult("WebUi", WhatIf ? "WhatIf" : "Success", WhatIf ? $"Would install WebUi from {nupkgPath}." : $"Installed WebUi from {nupkgPath}.");
        }
        catch (Exception ex)
        {
            return CreateDeploymentResult("WebUi", "Failed", ex.Message);
        }
    }

    private DeploymentResult DeployAndroidSelection(string targetName, bool expectEmulator, string requestedSerial)
    {
        try
        {
            if (!CommandExists("adb") && ResolveAdbPath() == "adb")
            {
                return CreateDeploymentResult(targetName, "Skipped", "adb was not found in PATH or Android SDK.");
            }

            var resolution = ResolveAndroidDevice(expectEmulator, requestedSerial);
            if (!string.Equals(resolution.Status, "Success", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDeploymentResult(targetName, resolution.Status, resolution.Message);
            }

            DeployAndroidCore(resolution.Serial);
            return CreateDeploymentResult(targetName, WhatIf ? "WhatIf" : "Success", WhatIf ? $"Would deploy to {resolution.Serial}." : $"Deployed to {resolution.Serial}.");
        }
        catch (Exception ex)
        {
            return CreateDeploymentResult(targetName, "Failed", ex.Message);
        }
    }

    private DeploymentResult DeployDesktopMsixCore()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return CreateDeploymentResult("DesktopMsix", "Skipped", "MSIX deployment is only available on Windows.");
            }

            var packagePath = BuildDesktopMsixCore(installAfterBuild: true);
            return CreateDeploymentResult("DesktopMsix", WhatIf ? "WhatIf" : "Success", WhatIf ? $"Would build/install {packagePath}." : $"Built and installed {packagePath}.");
        }
        catch (Exception ex)
        {
            return CreateDeploymentResult("DesktopMsix", "Failed", ex.Message);
        }
    }

    private DeploymentResult DeployDesktopDebCore()
    {
        try
        {
            if (OperatingSystem.IsWindows() && GetWslDistrosCore().Count == 0)
            {
                return CreateDeploymentResult("DesktopDeb", "Skipped", "WSL is not available.");
            }

            var debPath = BuildDesktopDebCore(installAfterBuild: true);
            return CreateDeploymentResult("DesktopDeb", WhatIf ? "WhatIf" : "Success", WhatIf ? $"Would build/install {debPath}." : $"Built and installed {debPath}.");
        }
        catch (Exception ex)
        {
            return CreateDeploymentResult("DesktopDeb", "Failed", ex.Message);
        }
    }

    private void WriteVersionInfo()
    {
        var version = ResolveVersionDetails(PackageVersion);
        WriteJsonToConsole(new
        {
            semver = version.SemVer,
            major = version.Major,
            minor = version.Minor,
            patch = version.Patch,
            commits = version.CommitsSinceVersionSource,
            versionCode = version.VersionCode
        });
    }

    private void RunBumpGitVersionPatch()
    {
        var gitVersionPath = Path.Combine(RepoRootPath, "GitVersion.yml");
        if (!File.Exists(gitVersionPath))
        {
            throw new FileNotFoundException($"GitVersion.yml not found at {gitVersionPath}");
        }

        var content = File.ReadAllText(gitVersionPath);
        var match = Regex.Match(content, @"(?m)^next-version:\s*(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not parse next-version from GitVersion.yml.");
        }

        var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var patch = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) + 1;
        var nextVersion = $"{major}.{minor}.{patch}";

        if (!ShouldExecuteAction($"Update GitVersion.yml next-version to {nextVersion}"))
        {
            return;
        }

        var updated = Regex.Replace(
            content,
            @"(?m)^(next-version:\s*)\d+\.\d+\.\d+",
            match => $"{match.Groups[1].Value}{nextVersion}");
        File.WriteAllText(gitVersionPath, updated);
        InvokeGit(new List<string> { "add", "GitVersion.yml" }, false);
    }

    private void RunUpdateDotnetToolTarget()
    {
        var resolvedProjectPath = ResolveProjectPathOrDefault(ProjectPath, Path.Combine("src", "McpServer.Director", "McpServer.Director.csproj"));
        var resolvedToolId = string.IsNullOrWhiteSpace(ToolId) ? "SharpNinja.McpServer.Director" : ToolId;
        var resolvedToolCommand = string.IsNullOrWhiteSpace(ToolCommand) ? "director" : ToolCommand;
        var packagePath = ExecuteDotnetToolPipeline(
            resolvedProjectPath,
            resolvedToolId,
            resolvedToolCommand,
            NupkgDir,
            installAfterPack: true,
            skipVersionBumpValue: SkipVersionBump,
            skipProcessStopValue: SkipProcessStop);
        Info($"Dotnet tool pipeline complete: {packagePath}");
    }

    private void RunPackDirectorToolTarget()
    {
        var packagePath = ExecuteDotnetToolPipeline(
            Path.Combine(RepoRootPath, "src", "McpServer.Director", "McpServer.Director.csproj"),
            "SharpNinja.McpServer.Director",
            "director",
            ArtifactsDirectoryPath,
            installAfterPack: false,
            skipVersionBumpValue: true,
            skipProcessStopValue: true);
        Info($"Director package ready: {packagePath}");
    }

    private void RunUpdateDirectorToolTarget()
    {
        var result = DeployDirectorCore();
        if (string.Equals(result.Status, "Failed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(result.Message);
        }

        Info(result.Message);
    }

    private void RunPublishWebZipTarget()
    {
        var version = ResolveVersionDetails(PackageVersion);
        var projectPath = Path.Combine(RepoRootPath, "src", "McpServer.Web", "McpServer.Web.csproj");
        var publishDirectory = Path.Combine(ArtifactsDirectoryPath, "web");
        var zipPath = Path.Combine(ArtifactsDirectoryPath, $"McpServer.Web-{version.SemVer}.zip");

        if (!ShouldExecuteAction($"Publish McpServer.Web {version.SemVer}"))
        {
            Info($"Would publish McpServer.Web to {zipPath}");
            return;
        }

        EnsureDirectoryExists(ArtifactsDirectoryPath);
        ClearDirectory(publishDirectory);
        InvokeDotNet(
            new List<string>
            {
                "publish",
                projectPath,
                "/p:AssemblyVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
                "/p:FileVersion=" + $"{version.Major}.{version.Minor}.{version.Patch}",
                "/p:InformationalVersion=" + version.SemVer,
                "-c",
                Configuration,
                "-p:Version=" + version.SemVer,
                "-o",
                publishDirectory
            },
            RepoRootPath);

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(publishDirectory, zipPath, CompressionLevel.Optimal, false);
        Info($"Web publish ready: {zipPath}");
    }

    private void RunUpdateWebUiToolTarget()
    {
        var result = DeployWebUiCore();
        if (string.Equals(result.Status, "Failed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(result.Message);
        }

        Info(result.Message);
    }

    private void RunBuildAndroidPackageTarget()
    {
        var apkPath = BuildAndroidPackageCore();
        Info($"Android package ready: {apkPath}");
    }

    private void RunDeployAndroidTarget()
    {
        var targetSerial = !string.IsNullOrWhiteSpace(DeviceSerial)
            ? DeviceSerial
            : !string.IsNullOrWhiteSpace(AndroidPhoneSerial)
                ? AndroidPhoneSerial
                : "ZD222QH58Q";
        DeployAndroidCore(targetSerial);
        Info($"Android deployment completed for {targetSerial}");
    }

    private void RunBuildDesktopMsixTarget()
    {
        var msixPath = BuildDesktopMsixCore(installAfterBuild: Install);
        Info($"Desktop MSIX ready: {msixPath}");
    }

    private void RunBuildDesktopDebTarget()
    {
        var debPath = BuildDesktopDebCore(installAfterBuild: Install);
        Info($"Desktop DEB ready: {debPath}");
    }

    private void RunDeployAllTarget()
    {
        var selections = ParseDeploySelections();
        var results = new List<DeploymentResult>();
        foreach (var selection in selections)
        {
            switch (selection)
            {
                case "Director":
                    results.Add(DeployDirectorCore());
                    break;
                case "WebUi":
                    results.Add(DeployWebUiCore());
                    break;
                case "AndroidPhone":
                    results.Add(DeployAndroidSelection("AndroidPhone", false, AndroidPhoneSerial));
                    break;
                case "AndroidEmulator":
                    results.Add(DeployAndroidSelection("AndroidEmulator", true, AndroidEmulatorSerial));
                    break;
                case "DesktopMsix":
                    results.Add(DeployDesktopMsixCore());
                    break;
                case "DesktopDeb":
                    results.Add(DeployDesktopDebCore());
                    break;
                default:
                    results.Add(CreateDeploymentResult(selection, "Failed", $"Unsupported deploy selection '{selection}'."));
                    break;
            }
        }

        ShowDeploySummary(results);
        if (results.Any(x => x.Status == "Failed"))
        {
            throw new InvalidOperationException("One or more deploy targets failed.");
        }
    }
}
