using Nuke.Common;

partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.DeployAll);

    [Parameter("Build configuration (Debug/Release)")] 
    readonly string Configuration = "Release";

    [Parameter("Override version for packages and artifacts")] 
    readonly string PackageVersion = string.Empty;

    [Parameter("Comma-separated list of components to deploy (All, Director, WebUi, AndroidPhone, AndroidEmulator, DesktopMsix, DesktopDeb)")] 
    readonly string DeploySelection = "All";

    [Parameter("Android device serial number for deployment and diagnostics")] 
    readonly string DeviceSerial = string.Empty;

    [Parameter("Serial number of physical Android phone")] 
    readonly string AndroidPhoneSerial = string.Empty;

    [Parameter("Serial number of Android emulator")] 
    readonly string AndroidEmulatorSerial = string.Empty;

    [Parameter("WSL distribution name for Linux packaging")] 
    readonly string WslDistro = string.Empty;

    [Parameter("Show what would be done without executing destructive actions")] 
    readonly bool WhatIf;

    [Parameter("Install package/extension after building")] 
    readonly bool Install;

    [Parameter("Skip automatic installation of VSIX/extension")] 
    readonly bool SkipInstall;

    [Parameter("Skip automatic GitVersion patch bump")] 
    readonly bool SkipVersionBump;

    [Parameter("Do not stop running processes before updating tools")] 
    readonly bool SkipProcessStop;

    [Parameter("Path to project used for tool packing/updating")] 
    readonly string ProjectPath = string.Empty;

    [Parameter("NuGet tool package ID (e.g. SharpNinja.McpServer.Director)")] 
    readonly string ToolId = string.Empty;

    [Parameter("Command name for the installed dotnet tool")] 
    readonly string ToolCommand = string.Empty;

    [Parameter("Directory for output NuGet packages")] 
    readonly string NupkgDir = "nupkg";

    [Parameter("Skip build step when possible")] 
    readonly bool NoBuild;

    [Parameter("Clean artifacts before building")] 
    readonly bool Clean;

    [Parameter("Force operation even when safety checks would block it")] 
    readonly bool Force;

    [Parameter("Skip code signing during packaging")] 
    readonly bool NoCert;

    [Parameter("Runtime Identifier (win-x64, linux-x64, etc.)")] 
    readonly string Rid = "win-x64";

    [Parameter("Root directory for build artifacts")] 
    readonly string OutputDir = string.Empty;

    [Parameter("Path where JSON output should be written")] 
    readonly string JsonOutputPath = string.Empty;

    [Parameter("Phase for Android crash collection workflow (Prepare/Collect)")] 
    readonly string Phase = "Collect";

    [Parameter("Android package name for diagnostics collection")] 
    readonly string PackageName = "ninja.thesharp.mcpservermanager";

    [Parameter("Root directory for specialized output (e.g. crash artifacts)")] 
    readonly string OutputRoot = string.Empty;

    [Parameter("Include full bugreport when collecting Android crash data")] 
    readonly bool IncludeBugreport;

    [Parameter("Port number for Web UI")] 
    readonly int Port = 5200;

    [Parameter("Timeout in seconds for startup/health checks")] 
    readonly int TimeoutSeconds = 60;

    [Parameter("Kill existing running instances before starting new ones")] 
    readonly bool KillExisting;

    [Parameter("Custom directory for installing VS extensions")] 
    readonly string InstallDir = string.Empty;

    [Parameter("Workspace folder to open when launching VS Code with extension")] 
    readonly string WorkspaceFolder = string.Empty;

    [Parameter("Path to the extension under development for VS Code launch")] 
    readonly string ExtensionDevelopmentPath = string.Empty;

    [Parameter("Path to VS Code (Code.exe) executable")] 
    readonly string VsCodePath = string.Empty;

    [Parameter("Path to a specific VSIX file to use")] 
    readonly string VsixPath = string.Empty;

    [Parameter("Specific file entry to extract/read from a VSIX archive")] 
    readonly string VsixEntry = string.Empty;

    [Parameter("Custom extensions directory for VS Code/Cursor")] 
    readonly string ExtensionsDir = string.Empty;

    [Parameter("Include installation for VS Code")] 
    readonly bool IncludeVsCode = true;

    [Parameter("Include installation for Cursor editor")] 
    readonly bool IncludeCursor = true;

    [Parameter("Expected version for validation steps")] 
    readonly string ExpectedVersion = string.Empty;

    [Parameter("Expected F-Droid repository URL for verification")] 
    readonly string ExpectedRepoUrl = string.Empty;

    // Targets sorted alphabetically by name (updated for consistent help output)
    Target BuildAndInstallVsix => _ => _.Executes(RunBuildAndInstallVsixTarget);

    Target BuildAndroidPackage => _ => _.Executes(RunBuildAndroidPackageTarget);

    Target BuildDesktopDeb => _ => _.Executes(RunBuildDesktopDebTarget);

    Target BuildDesktopMsix => _ => _.Executes(RunBuildDesktopMsixTarget);

    Target BumpGitVersionPatch => _ => _.Executes(RunBumpGitVersionPatch);

    Target CheckPackageVersions => _ => _.Executes(RunCheckPackageVersionsTarget);

    Target CollectAndroidCrashArtifacts => _ => _.Executes(RunCollectAndroidCrashArtifactsTarget);

    Target DeployAll => _ => _.Executes(RunDeployAllTarget);

    Target DeployAndroid => _ => _.Executes(RunDeployAndroidTarget);

    Target DeployMcpTodoExtension => _ => _.Executes(RunDeployMcpTodoExtensionTarget);

    Target GenerateFdroidRepo => _ => _.Executes(RunGenerateFdroidRepoTarget);

    Target InstallMcpServerMcpTodoVsix => _ => _.Executes(RunInstallMcpServerMcpTodoVsixTarget);

    Target LaunchVsCodeExtension => _ => _.Executes(RunLaunchVsCodeExtensionTarget);

    Target ListVsix => _ => _.Executes(RunListVsixTarget);

    Target PackDirectorTool => _ => _.Executes(RunPackDirectorToolTarget);

    Target PackageVsix => _ => _.Executes(RunPackageVsixTarget);

    Target PreparePagesArtifact => _ => _.Executes(RunPreparePagesArtifactTarget);

    Target PublishWebZip => _ => _.Executes(RunPublishWebZipTarget);

    Target ReadVsix => _ => _.Executes(RunReadVsixTarget);

    Target StartWebUi => _ => _.Executes(RunStartWebUiTarget);

    Target UpdateDirectorTool => _ => _.Executes(RunUpdateDirectorToolTarget);

    Target UpdateDotnetTool => _ => _.Executes(RunUpdateDotnetToolTarget);

    Target UpdateWebUiTool => _ => _.Executes(RunUpdateWebUiToolTarget);

    Target VerifyFdroidRepo => _ => _.Executes(RunVerifyFdroidRepoTarget);

    Target VersionInfo => _ => _.Executes(WriteVersionInfo);
}
