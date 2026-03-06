#Requires -Version 5.1
<#
.SYNOPSIS
  Builds the McpServer MCP Todo Visual Studio extension (VSIX) with MSBuild and optionally installs it.
.DESCRIPTION
  Builds the legacy VSIX project (McpServer.VsExtension.McpTodo.Vsix) with Visual Studio MSBuild.
  Restore + Build produces the VSIX in bin\<Configuration>\. You can also build from Visual Studio:
  open the solution, build the McpServer.VsExtension.McpTodo.Vsix project, then install the .vsix from bin\Debug or bin\Release.
.EXAMPLE
  .\Build-AndInstall-Vsix.ps1
  .\Build-AndInstall-Vsix.ps1 -Configuration Release
  .\Build-AndInstall-Vsix.ps1 -SkipInstall
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $SkipInstall
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$extDir = Join-Path $repoRoot "src\McpServer.VsExtension.McpTodo.Vsix"
$outDir = Join-Path $extDir "bin\$Configuration"
$vsixName = "McpServer.VsExtension.McpTodo.vsix"
$vsixPath = Join-Path $outDir $vsixName
$csproj = Join-Path $extDir "McpServer.VsExtension.McpTodo.Vsix.csproj"

# Build with dotnet
Write-Host "Building VSIX project with dotnet (Configuration=$Configuration)..." -ForegroundColor Cyan
Push-Location $repoRoot
try {
    dotnet build $csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
} finally { Pop-Location }

# Package VSIX manually since SDK build doesn't do it automatically here
Write-Host "Packaging VSIX..." -ForegroundColor Cyan
$packageScript = Join-Path $scriptDir "Package-Vsix.ps1"
& $packageScript -Configuration $Configuration

# Updated VSIX path for SDK-style project
$vsixPath = Join-Path $outDir "net472\win\$vsixName"

if (-not (Test-Path $vsixPath)) {
    throw "VSIX not found at $vsixPath after packaging."
}
Write-Host "VSIX created: $vsixPath" -ForegroundColor Green

if (-not $SkipInstall) {
    Write-Host "Launching VSIX installer..." -ForegroundColor Cyan
    Start-Process -FilePath $vsixPath
    Write-Host "Complete installation in the dialog, then use View > Other Windows > MCP Todo in Visual Studio." -ForegroundColor Green
} else {
    Write-Host "Skipping install (omit -SkipInstall to install)." -ForegroundColor Yellow
}
