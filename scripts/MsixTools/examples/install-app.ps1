#Requires -Version 7.0
<#
.SYNOPSIS
  Example wrapper — install or uninstall an MSIX package using MsixTools.
  Copy this to your project's scripts/ folder and customise as needed.

.PARAMETER MsixPath
  Path to the .msix file. Auto-detected from artifacts/ if omitted.

.PARAMETER CertPath
  Path to a .cer file to trust before installing.

.PARAMETER TimeoutSeconds
  Seconds to wait for the Windows service to register. Default: 30.

.PARAMETER Uninstall
  Stop the service and remove the package.

.EXAMPLE
  .\scripts\install-app.ps1
  .\scripts\install-app.ps1 -MsixPath artifacts\myapp_1.0.0_x64.msix
  .\scripts\install-app.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [string] $MsixPath = "",
    [string] $CertPath = "",
    [int]    $TimeoutSeconds = 30,
    [switch] $Uninstall
)

$ErrorActionPreference = "Stop"
$WorkspaceRoot = Convert-Path (Split-Path $PSScriptRoot -Parent)
$ModulePath    = Join-Path $PSScriptRoot "MsixTools\MsixTools.psd1"
if (-not (Test-Path $ModulePath)) {
    $ModulePath = Join-Path (Split-Path $WorkspaceRoot -Parent) "MsixTools\MsixTools.psd1"
}
if (-not (Test-Path $ModulePath)) { Write-Error "MsixTools module not found. Run: git submodule update --init" }
Import-Module $ModulePath -Force

$configPath = Join-Path $WorkspaceRoot "msix.yml"

if ($Uninstall) {
    Uninstall-MsixPackage -ConfigPath $configPath
    exit 0
}

$params = @{ ConfigPath = $configPath; TimeoutSeconds = $TimeoutSeconds }
if ($MsixPath) { $params['MsixPath'] = $MsixPath }
if ($CertPath) { $params['CertPath'] = $CertPath }

Install-MsixPackage @params
