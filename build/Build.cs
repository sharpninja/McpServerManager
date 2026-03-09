using Nuke.Common;

partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.DeployAll);

    [Parameter] readonly string Configuration = "Release";
    [Parameter] readonly string PackageVersion = string.Empty;
    [Parameter] readonly string DeploySelection = "All";
    [Parameter] readonly string DeviceSerial = string.Empty;
    [Parameter] readonly string AndroidPhoneSerial = string.Empty;
    [Parameter] readonly string AndroidEmulatorSerial = string.Empty;
    [Parameter] readonly string WslDistro = string.Empty;
    [Parameter] readonly bool WhatIf;
    [Parameter] readonly bool Install;
    [Parameter] readonly bool SkipInstall;
    [Parameter] readonly bool SkipVersionBump;
    [Parameter] readonly bool SkipProcessStop;
    [Parameter] readonly string ProjectPath = string.Empty;
    [Parameter] readonly string ToolId = string.Empty;
    [Parameter] readonly string ToolCommand = string.Empty;
    [Parameter] readonly string NupkgDir = "nupkg";
    [Parameter] readonly bool NoBuild;
    [Parameter] readonly bool Clean;
    [Parameter] readonly bool Force;
    [Parameter] readonly bool NoCert;
    [Parameter] readonly string Rid = "win-x64";
    [Parameter] readonly string OutputDir = string.Empty;
    [Parameter] readonly string JsonOutputPath = string.Empty;
    [Parameter] readonly string Phase = "Collect";
    [Parameter] readonly string PackageName = "ninja.thesharp.mcpservermanager";
    [Parameter] readonly string OutputRoot = string.Empty;
    [Parameter] readonly bool IncludeBugreport;
    [Parameter] readonly int Port = 5200;
    [Parameter] readonly int TimeoutSeconds = 60;
    [Parameter] readonly bool KillExisting;
    [Parameter] readonly string InstallDir = string.Empty;
    [Parameter] readonly string WorkspaceFolder = string.Empty;
    [Parameter] readonly string ExtensionDevelopmentPath = string.Empty;
    [Parameter] readonly string VsCodePath = string.Empty;
    [Parameter] readonly string VsixPath = string.Empty;
    [Parameter] readonly string VsixEntry = string.Empty;
    [Parameter] readonly string ExtensionsDir = string.Empty;
    [Parameter] readonly bool IncludeVsCode = true;
    [Parameter] readonly bool IncludeCursor = true;
    [Parameter] readonly string ExpectedVersion = string.Empty;
    [Parameter] readonly string ExpectedRepoUrl = string.Empty;

    Target VersionInfo => _ => _
        .Executes(WriteVersionInfo);

    Target BumpGitVersionPatch => _ => _
        .Executes(RunBumpGitVersionPatch);

    Target UpdateDotnetTool => _ => _
        .Executes(RunUpdateDotnetToolTarget);

    Target PackDirectorTool => _ => _
        .Executes(RunPackDirectorToolTarget);

    Target UpdateDirectorTool => _ => _
        .Executes(RunUpdateDirectorToolTarget);

    Target PublishWebZip => _ => _
        .Executes(RunPublishWebZipTarget);

    Target UpdateWebUiTool => _ => _
        .Executes(RunUpdateWebUiToolTarget);

    Target BuildAndroidPackage => _ => _
        .Executes(RunBuildAndroidPackageTarget);

    Target DeployAndroid => _ => _
        .Executes(RunDeployAndroidTarget);

    Target BuildDesktopMsix => _ => _
        .Executes(RunBuildDesktopMsixTarget);

    Target BuildDesktopDeb => _ => _
        .Executes(RunBuildDesktopDebTarget);

    Target PackageVsix => _ => _
        .Executes(RunPackageVsixTarget);

    Target BuildAndInstallVsix => _ => _
        .Executes(RunBuildAndInstallVsixTarget);

    Target InstallMcpServerMcpTodoVsix => _ => _
        .Executes(RunInstallMcpServerMcpTodoVsixTarget);

    Target DeployMcpTodoExtension => _ => _
        .Executes(RunDeployMcpTodoExtensionTarget);

    Target CollectAndroidCrashArtifacts => _ => _
        .Executes(RunCollectAndroidCrashArtifactsTarget);

    Target StartWebUi => _ => _
        .Executes(RunStartWebUiTarget);

    Target CheckPackageVersions => _ => _
        .Executes(RunCheckPackageVersionsTarget);

    Target LaunchVsCodeExtension => _ => _
        .Executes(RunLaunchVsCodeExtensionTarget);

    Target ListVsix => _ => _
        .Executes(RunListVsixTarget);

    Target ReadVsix => _ => _
        .Executes(RunReadVsixTarget);

    Target GenerateFdroidRepo => _ => _
        .Executes(RunGenerateFdroidRepoTarget);

    Target VerifyFdroidRepo => _ => _
        .Executes(RunVerifyFdroidRepoTarget);

    Target PreparePagesArtifact => _ => _
        .Executes(RunPreparePagesArtifactTarget);

    Target DeployAll => _ => _
        .Executes(RunDeployAllTarget);
}
