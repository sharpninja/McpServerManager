using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Runtime.Versioning;
using System.Xml;
using System.Xml.Linq;
using static Nuke.Common.Logger;

partial class Build
{
    private sealed class MsixDesktopConfiguration
    {
        public string PackageName { get; set; } = "McpServerManager";
        public string PackageDisplayName { get; set; } = "McpServerManager";
        public string Publisher { get; set; } = string.Empty;
        public string DesktopProjectPath { get; set; } = Path.Combine("src", "McpServerManager.Desktop", "McpServerManager.Desktop.csproj");
        public string DesktopFramework { get; set; } = "net9.0";
        public string DesktopSubDirectory { get; set; } = "desktop";
        public string DesktopAppId { get; set; } = "McpServerManagerDesktop";
        public string DesktopDescription { get; set; } = "Avalonia desktop app for browsing and analyzing Copilot request/session logs";
        public string RuntimeId { get; set; } = "win-x64";
        public bool SelfContained { get; set; } = true;
        public string OutputDirectory { get; set; } = "artifacts";
        public string IconSourcePath { get; set; } = Path.Combine("src", "McpServerManager.Core", "Assets", "logo.svg");
    }

    private sealed class MsixIconSpec
    {
        public MsixIconSpec(string fileName, int width, int height)
        {
            FileName = fileName;
            Width = width;
            Height = height;
        }

        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
    }

    [SupportedOSPlatform("windows")]
    private string BuildDesktopMsixCoreNative(bool installAfterBuild)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("MSIX packaging is only available on Windows.");
        }

        if (installAfterBuild && !IsWindowsAdministrator())
        {
            throw new InvalidOperationException("MSIX installation requires an elevated PowerShell session.");
        }

        if (installAfterBuild && NoCert)
        {
            throw new InvalidOperationException("MSIX installation requires signing. Re-run without --no-cert.");
        }

        var configPath = Path.Combine(RepoRootPath, "msix.yml");
        var config = ReadMsixDesktopConfiguration(configPath);
        var version = ResolveVersionDetails(PackageVersion);
        var runtimeId = ResolveDesktopMsixRid(config.RuntimeId);
        var outputDirectory = !string.IsNullOrWhiteSpace(OutputDir)
            ? ResolveOutputDirectory()
            : (Path.IsPathRooted(config.OutputDirectory) ? config.OutputDirectory : Path.Combine(RepoRootPath, config.OutputDirectory));
        var msixPath = Path.Combine(outputDirectory, $"{config.PackageName}-{version.SemVer}-{runtimeId}.msix");

        if (!ShouldExecuteAction($"Build desktop MSIX {version.SemVer}"))
        {
            return msixPath;
        }

        var makeAppxPath = FindWindowsSdkTool("makeappx.exe");
        if (string.IsNullOrWhiteSpace(makeAppxPath))
        {
            throw new FileNotFoundException("makeappx.exe was not found. Install a compatible Windows SDK.");
        }

        var signToolPath = !NoCert || installAfterBuild ? FindWindowsSdkTool("signtool.exe") : string.Empty;
        if ((!NoCert || installAfterBuild) && string.IsNullOrWhiteSpace(signToolPath))
        {
            throw new FileNotFoundException("signtool.exe was not found. Install a compatible Windows SDK.");
        }

        EnsureDirectoryExists(outputDirectory);

        var projectPath = ResolveProjectPathOrDefault(config.DesktopProjectPath, config.DesktopProjectPath);
        if (Clean)
        {
            ClearProjectBuildOutputs(projectPath);
        }

        var publishDirectory = Path.Combine(outputDirectory, "publish-desktop");
        if (!NoBuild)
        {
            ClearDirectory(publishDirectory);
            var publishArguments = new List<string>
            {
                "publish",
                projectPath,
                "-c",
                Configuration,
                "-r",
                runtimeId,
                "-f",
                config.DesktopFramework,
                "-o",
                publishDirectory,
                "-p:Version=" + version.SemVer,
                "-p:AssemblyVersion=" + ConvertToMsixVersion(version.SemVer),
                "-p:FileVersion=" + ConvertToMsixVersion(version.SemVer),
                "-p:InformationalVersion=" + version.SemVer
            };

            if (config.SelfContained)
            {
                publishArguments.Add("--self-contained");
                publishArguments.Add("true");
                publishArguments.Add("-p:PublishSingleFile=true");
                publishArguments.Add("-p:IncludeNativeLibrariesForSelfExtract=true");
            }
            else
            {
                publishArguments.Add("--self-contained");
                publishArguments.Add("false");
            }

            InvokeDotNet(publishArguments, RepoRootPath);
        }
        else if (!Directory.Exists(publishDirectory))
        {
            throw new DirectoryNotFoundException($"Publish output not found at {publishDirectory}");
        }

        var layoutDirectory = Path.Combine(outputDirectory, "msix-layout");
        var assetsDirectory = Path.Combine(layoutDirectory, "Assets");
        ClearDirectory(layoutDirectory);
        EnsureDirectoryExists(assetsDirectory);

        var desktopSubDirectory = Path.Combine(layoutDirectory, config.DesktopSubDirectory);
        EnsureDirectoryExists(desktopSubDirectory);
        CopyDirectoryContents(publishDirectory, desktopSubDirectory);

        var executableName = ResolveDesktopExecutableName(projectPath, publishDirectory);
        WriteMsixIconAssets(assetsDirectory, config.IconSourcePath);
        WriteDesktopMsixManifest(
            Path.Combine(layoutDirectory, "AppxManifest.xml"),
            config,
            version.SemVer,
            runtimeId,
            executableName);

        if (File.Exists(msixPath))
        {
            File.Delete(msixPath);
        }

        InvokeProcess(
            makeAppxPath,
            new List<string> { "pack", "/d", layoutDirectory, "/p", msixPath, "/o", "/nv" },
            RepoRootPath,
            true);

        X509Certificate2? certificate = null;
        if (!NoCert)
        {
            var certificatePath = Path.Combine(outputDirectory, $"{config.PackageName}-dev.cer");
            certificate = EnsureCodeSigningCertificate(config.Publisher, $"{config.PackageDisplayName} Dev Certificate", certificatePath);
            SignMsixPackage(signToolPath!, msixPath, certificate.Thumbprint);
        }

        if (installAfterBuild)
        {
            certificate ??= EnsureCodeSigningCertificate(
                config.Publisher,
                $"{config.PackageDisplayName} Dev Certificate",
                Path.Combine(outputDirectory, $"{config.PackageName}-dev.cer"));
            TrustCodeSigningCertificate(certificate, StoreLocation.LocalMachine);
            InstallMsixPackage(config.PackageName, msixPath);
        }

        return msixPath;
    }

    private MsixDesktopConfiguration ReadMsixDesktopConfiguration(string configPath)
    {
        var config = new MsixDesktopConfiguration();
        if (!File.Exists(configPath))
        {
            config.Publisher = $"CN={config.PackageName} Dev";
            return config;
        }

        var currentSection = string.Empty;
        foreach (var rawLine in File.ReadAllLines(configPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine[0]))
            {
                currentSection = trimmed.EndsWith(":", StringComparison.Ordinal)
                    ? trimmed[..^1].Trim()
                    : string.Empty;
                continue;
            }

            var parts = trimmed.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = UnquoteYamlScalar(parts[1]);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "[]", StringComparison.Ordinal))
            {
                continue;
            }

            switch (currentSection)
            {
                case "package":
                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        config.PackageName = value;
                    }
                    else if (string.Equals(key, "displayName", StringComparison.OrdinalIgnoreCase))
                    {
                        config.PackageDisplayName = value;
                    }
                    else if (string.Equals(key, "publisher", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Publisher = value;
                    }
                    break;
                case "desktop":
                    if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                    {
                        config.DesktopProjectPath = value;
                    }
                    else if (string.Equals(key, "framework", StringComparison.OrdinalIgnoreCase))
                    {
                        config.DesktopFramework = value;
                    }
                    else if (string.Equals(key, "subDir", StringComparison.OrdinalIgnoreCase))
                    {
                        config.DesktopSubDirectory = value;
                    }
                    else if (string.Equals(key, "appId", StringComparison.OrdinalIgnoreCase))
                    {
                        config.DesktopAppId = value;
                    }
                    else if (string.Equals(key, "description", StringComparison.OrdinalIgnoreCase))
                    {
                        config.DesktopDescription = value;
                    }
                    break;
                case "build":
                    if (string.Equals(key, "rid", StringComparison.OrdinalIgnoreCase))
                    {
                        config.RuntimeId = value;
                    }
                    else if (string.Equals(key, "selfContained", StringComparison.OrdinalIgnoreCase))
                    {
                        config.SelfContained = ParseYamlBool(value, true);
                    }
                    break;
                case "output":
                    if (string.Equals(key, "dir", StringComparison.OrdinalIgnoreCase))
                    {
                        config.OutputDirectory = value;
                    }
                    break;
                case "icons":
                    if (string.Equals(key, "svg", StringComparison.OrdinalIgnoreCase))
                    {
                        config.IconSourcePath = value;
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(config.Publisher))
        {
            config.Publisher = $"CN={config.PackageName} Dev";
        }

        return config;
    }

    private string ResolveDesktopMsixRid(string configuredRuntimeId)
    {
        var runtimeId = string.IsNullOrWhiteSpace(Rid) ? configuredRuntimeId : Rid;
        if (string.IsNullOrWhiteSpace(runtimeId))
        {
            runtimeId = "win-x64";
        }

        if (!runtimeId.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"MSIX packaging requires a Windows RID, but '{runtimeId}' was provided.");
        }

        return runtimeId;
    }

    private string ResolveDesktopMsixOutputPath()
    {
        return ResolveDesktopMsixPackagingContext().MsixPath;
    }

    private (MsixDesktopConfiguration Config, VersionDetails Version, string RuntimeId, string OutputDirectory, string MsixPath, string CertificatePath)
        ResolveDesktopMsixPackagingContext()
    {
        var configPath = Path.Combine(RepoRootPath, "msix.yml");
        var config = ReadMsixDesktopConfiguration(configPath);
        var version = ResolveVersionDetails(PackageVersion);
        var runtimeId = ResolveDesktopMsixRid(config.RuntimeId);
        var outputDirectory = !string.IsNullOrWhiteSpace(OutputDir)
            ? ResolveOutputDirectory()
            : (Path.IsPathRooted(config.OutputDirectory) ? config.OutputDirectory : Path.Combine(RepoRootPath, config.OutputDirectory));

        return (
            config,
            version,
            runtimeId,
            outputDirectory,
            Path.Combine(outputDirectory, $"{config.PackageName}-{version.SemVer}-{runtimeId}.msix"),
            Path.Combine(outputDirectory, $"{config.PackageName}-dev.cer"));
    }

    [SupportedOSPlatform("windows")]
    private string BuildDesktopMsixWithElevation()
    {
        if (NoCert)
        {
            throw new InvalidOperationException("MSIX installation requires signing. Re-run without --no-cert.");
        }

        if (WhatIf)
        {
            if (CommandExists("gsudo"))
            {
                Warn("[WhatIf] Elevate the MSIX certificate trust/install step through gsudo.");
            }
            else
            {
                Warn("[WhatIf] MSIX installation would require elevation, and gsudo was not found in PATH.");
            }

            return BuildDesktopMsixCoreNative(installAfterBuild: false);
        }

        if (!CommandExists("gsudo"))
        {
            throw new InvalidOperationException("MSIX installation requires elevation, but gsudo was not found in PATH. Install gsudo or rerun this command from an elevated PowerShell session.");
        }

        var packagingContext = ResolveDesktopMsixPackagingContext();
        var msixPath = BuildDesktopMsixCoreNative(installAfterBuild: false);
        InstallMsixPackageWithGsudo(packagingContext.Config.PackageName, msixPath, packagingContext.CertificatePath);
        return msixPath;
    }

    [SupportedOSPlatform("windows")]
    private void InstallMsixPackageWithGsudo(string packageName, string msixPath, string certificatePath)
    {
        var command = string.Join(
            Environment.NewLine,
            new[]
            {
                $"$certPath = '{QuotePowerShellLiteral(certificatePath)}'",
                $"$msixPath = '{QuotePowerShellLiteral(msixPath)}'",
                $"$packageName = '{QuotePowerShellLiteral(packageName)}'",
                "if (-not (Test-Path -LiteralPath $certPath)) { throw \"Signing certificate not found at $certPath\" }",
                "if (-not (Test-Path -LiteralPath $msixPath)) { throw \"MSIX package not found at $msixPath\" }",
                "$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $certPath",
                "$trustedCertificate = Get-ChildItem Cert:\\LocalMachine\\Root | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1",
                "if ($null -eq $trustedCertificate) { Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\\LocalMachine\\Root' | Out-Null }",
                "$existingPackage = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue",
                "if ($null -ne $existingPackage) { Remove-AppxPackage -Package $existingPackage.PackageFullName }",
                "Add-AppxPackage -Path $msixPath"
            });

        var result = InvokeProcess(
            "gsudo",
            new List<string>
            {
                "--wait",
                "--chdir",
                RepoRootPath,
                "powershell.exe",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                command
            },
            RepoRootPath,
            false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"gsudo failed to elevate MSIX installation (exit code {result.ExitCode}).{Environment.NewLine}{result.GetCombinedOutput()}");
        }
    }

    private static string UnquoteYamlScalar(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
             (trimmed.StartsWith('\'') && trimmed.EndsWith('\''))))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static bool ParseYamlBool(string value, bool fallback)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private string FindWindowsSdkTool(string executableName)
    {
        var sdkRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "bin");
        if (Directory.Exists(sdkRoot))
        {
            var candidates = Directory
                .EnumerateDirectories(sdkRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(path => Path.Combine(path, "x64", executableName))
                .Where(File.Exists)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (candidates.Count > 0)
            {
                return candidates[0];
            }
        }

        if (CommandExists(executableName))
        {
            return executableName;
        }

        return string.Empty;
    }

    private void ClearProjectBuildOutputs(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return;
        }

        foreach (var directoryName in new[] { "bin", "obj" })
        {
            var targetDirectory = Path.Combine(projectDirectory, directoryName);
            if (Directory.Exists(targetDirectory))
            {
                ClearDirectory(targetDirectory);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private string ResolveDesktopExecutableName(string projectPath, string publishDirectory)
    {
        var assemblyName = ResolveProjectAssemblyName(projectPath);
        var candidate = Path.Combine(publishDirectory, $"{assemblyName}.exe");
        if (File.Exists(candidate))
        {
            return Path.GetFileName(candidate);
        }

        var extensionlessCandidate = Path.Combine(publishDirectory, assemblyName);
        if (File.Exists(extensionlessCandidate))
        {
            File.Copy(extensionlessCandidate, candidate, true);
            return Path.GetFileName(candidate);
        }

        var fallback = Directory
            .EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            throw new FileNotFoundException($"Could not locate the desktop executable in {publishDirectory}");
        }

        return Path.GetFileName(fallback);
    }

    private string ResolveProjectAssemblyName(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var assemblyName = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "AssemblyName", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            return assemblyName;
        }

        return Path.GetFileNameWithoutExtension(projectPath);
    }

    [SupportedOSPlatform("windows")]
    private void WriteMsixIconAssets(string assetsDirectory, string iconSourcePath)
    {
        var iconSourceDirectory = Path.GetDirectoryName(
            Path.IsPathRooted(iconSourcePath)
                ? iconSourcePath
                : Path.Combine(RepoRootPath, iconSourcePath)) ?? Path.Combine(RepoRootPath, "src", "McpServerManager.Core", "Assets");
        var sourcePngPath = ResolveMsixPngSource(iconSourceDirectory);
        if (string.IsNullOrWhiteSpace(sourcePngPath))
        {
            throw new FileNotFoundException($"Could not locate a PNG icon source in {iconSourceDirectory}");
        }

        var iconSpecs = new List<MsixIconSpec>
        {
            new("Square44x44Logo.png", 44, 44),
            new("Square150x150Logo.png", 150, 150),
            new("Wide310x150Logo.png", 310, 150),
            new("Square310x310Logo.png", 310, 310),
            new("StoreLogo.png", 50, 50)
        };

        foreach (var spec in iconSpecs)
        {
            WriteResizedPngAsset(sourcePngPath, Path.Combine(assetsDirectory, spec.FileName), spec.Width, spec.Height);
        }
    }

    [SupportedOSPlatform("windows")]
    private string ResolveMsixPngSource(string iconSourceDirectory)
    {
        if (!Directory.Exists(iconSourceDirectory))
        {
            return string.Empty;
        }

        var candidates = Directory
            .EnumerateFiles(iconSourceDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => ExtractPngResolutionWeight(path))
            .ThenByDescending(path => new FileInfo(path).Length)
            .ToList();
        return candidates.Count > 0 ? candidates[0] : string.Empty;
    }

    private static int ExtractPngResolutionWeight(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d+)(?=\.png$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var weight))
        {
            return weight;
        }

        return 0;
    }

    [SupportedOSPlatform("windows")]
    private void WriteResizedPngAsset(string sourcePngPath, string destinationPath, int width, int height)
    {
        using var sourceImage = Image.FromFile(sourcePngPath);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var scale = Math.Min((double)width / sourceImage.Width, (double)height / sourceImage.Height);
        var destinationWidth = Math.Max(1, (int)Math.Round(sourceImage.Width * scale));
        var destinationHeight = Math.Max(1, (int)Math.Round(sourceImage.Height * scale));
        var destinationX = (width - destinationWidth) / 2;
        var destinationY = (height - destinationHeight) / 2;

        graphics.DrawImage(sourceImage, destinationX, destinationY, destinationWidth, destinationHeight);
        bitmap.Save(destinationPath, ImageFormat.Png);
    }

    private void WriteDesktopMsixManifest(
        string manifestPath,
        MsixDesktopConfiguration config,
        string semVer,
        string runtimeId,
        string executableName)
    {
        var foundationNamespace = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/foundation/windows10");
        var uapNamespace = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/uap/windows10");
        var restrictedNamespace = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities");
        var desktopExecutable = $"{config.DesktopSubDirectory}\\{executableName}";

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                foundationNamespace + "Package",
                new XAttribute(XNamespace.Xmlns + "uap", uapNamespace),
                new XAttribute(XNamespace.Xmlns + "rescap", restrictedNamespace),
                new XAttribute("IgnorableNamespaces", "rescap"),
                new XElement(
                    foundationNamespace + "Identity",
                    new XAttribute("Name", config.PackageName),
                    new XAttribute("Publisher", config.Publisher),
                    new XAttribute("Version", ConvertToMsixVersion(semVer)),
                    new XAttribute("ProcessorArchitecture", ResolveMsixArchitecture(runtimeId))),
                new XElement(
                    foundationNamespace + "Properties",
                    new XElement(foundationNamespace + "DisplayName", config.PackageDisplayName),
                    new XElement(foundationNamespace + "PublisherDisplayName", config.PackageDisplayName),
                    new XElement(foundationNamespace + "Logo", @"Assets\StoreLogo.png")),
                new XElement(
                    foundationNamespace + "Dependencies",
                    new XElement(
                        foundationNamespace + "TargetDeviceFamily",
                        new XAttribute("Name", "Windows.Desktop"),
                        new XAttribute("MinVersion", "10.0.19041.0"),
                        new XAttribute("MaxVersionTested", "10.0.26100.0"))),
                new XElement(
                    foundationNamespace + "Resources",
                    new XElement(foundationNamespace + "Resource", new XAttribute("Language", "en-us"))),
                new XElement(
                    foundationNamespace + "Capabilities",
                    new XElement(restrictedNamespace + "Capability", new XAttribute("Name", "runFullTrust")),
                    new XElement(restrictedNamespace + "Capability", new XAttribute("Name", "allowElevation"))),
                new XElement(
                    foundationNamespace + "Applications",
                    new XElement(
                        foundationNamespace + "Application",
                        new XAttribute("Id", config.DesktopAppId),
                        new XAttribute("Executable", desktopExecutable),
                        new XAttribute("EntryPoint", "Windows.FullTrustApplication"),
                        new XElement(
                            uapNamespace + "VisualElements",
                            new XAttribute("DisplayName", config.PackageDisplayName),
                            new XAttribute("Description", config.DesktopDescription),
                            new XAttribute("BackgroundColor", "transparent"),
                            new XAttribute("Square150x150Logo", @"Assets\Square150x150Logo.png"),
                            new XAttribute("Square44x44Logo", @"Assets\Square44x44Logo.png"),
                            new XElement(
                                uapNamespace + "DefaultTile",
                                new XAttribute("Wide310x150Logo", @"Assets\Wide310x150Logo.png"),
                                new XAttribute("Square310x310Logo", @"Assets\Square310x310Logo.png")))))));

        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = true
        };
        using var writer = XmlWriter.Create(manifestPath, writerSettings);
        document.Save(writer);
    }

    private static string ConvertToMsixVersion(string semVer)
    {
        var versionPart = semVer.Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        var segments = versionPart.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (segments.Count < 4)
        {
            segments.Add("0");
        }

        return string.Join(".", segments.Take(4));
    }

    private static string ResolveMsixArchitecture(string runtimeId)
    {
        return runtimeId switch
        {
            "win-x86" => "x86",
            "win-arm64" => "arm64",
            _ => "x64"
        };
    }

    [SupportedOSPlatform("windows")]
    private X509Certificate2 EnsureCodeSigningCertificate(string publisher, string friendlyName, string certificatePath)
    {
        var existing = FindCodeSigningCertificate(publisher);
        if (existing is not null)
        {
            File.WriteAllBytes(certificatePath, existing.Export(X509ContentType.Cert));
            return existing;
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName(publisher),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") },
                false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        var persisted = X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pfx),
            string.Empty,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(persisted);

        File.WriteAllBytes(certificatePath, persisted.Export(X509ContentType.Cert));
        return new X509Certificate2(persisted);
    }

    [SupportedOSPlatform("windows")]
    private X509Certificate2? FindCodeSigningCertificate(string publisher)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates
            .OfType<X509Certificate2>()
            .Where(certificate =>
                certificate.HasPrivateKey &&
                certificate.NotAfter > DateTime.UtcNow &&
                string.Equals(certificate.Subject, publisher, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(certificate => certificate.NotAfter)
            .Select(certificate => new X509Certificate2(certificate))
            .FirstOrDefault();
    }

    [SupportedOSPlatform("windows")]
    private void SignMsixPackage(string signToolPath, string msixPath, string thumbprint)
    {
        var timestampedResult = InvokeProcess(
            signToolPath,
            new List<string>
            {
                "sign",
                "/sha1",
                thumbprint,
                "/fd",
                "SHA256",
                "/tr",
                "http://timestamp.digicert.com",
                "/td",
                "sha256",
                msixPath
            },
            RepoRootPath,
            false);
        if (timestampedResult.ExitCode == 0)
        {
            return;
        }

        var fallbackResult = InvokeProcess(
            signToolPath,
            new List<string>
            {
                "sign",
                "/sha1",
                thumbprint,
                "/fd",
                "SHA256",
                msixPath
            },
            RepoRootPath,
            false);
        if (fallbackResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"signtool failed.{Environment.NewLine}{fallbackResult.GetCombinedOutput()}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void TrustCodeSigningCertificate(X509Certificate2 certificate, StoreLocation location)
    {
        using var store = new X509Store(StoreName.Root, location);
        store.Open(OpenFlags.ReadWrite);
        var existing = store.Certificates
            .OfType<X509Certificate2>()
            .Any(item => string.Equals(item.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
        if (!existing)
        {
            store.Add(certificate);
        }
    }

    [SupportedOSPlatform("windows")]
    private void InstallMsixPackage(string packageName, string msixPath)
    {
        var command = string.Join(
            Environment.NewLine,
            new[]
            {
                $"$existing = Get-AppxPackage -Name '{QuotePowerShellLiteral(packageName)}' -ErrorAction SilentlyContinue",
                "if ($null -ne $existing) { Remove-AppxPackage -Package $existing.PackageFullName }",
                $"Add-AppxPackage -Path '{QuotePowerShellLiteral(msixPath)}'"
            });
        InvokePowerShellCommand(command, true);
    }
}
