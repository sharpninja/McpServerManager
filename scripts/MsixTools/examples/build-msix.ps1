#Requires -Version 7.0
<#
.SYNOPSIS
  Example wrapper — build an MSIX package using MsixTools.
  Copy to your project's scripts/ folder; adjust module path if needed.

.PARAMETER Configuration  Build configuration: Release (default) or Debug.
.PARAMETER SelfContained  Bundle the .NET runtime. Default: true.
.PARAMETER Install        Install the MSIX and start the service. Requires Administrator.
.PARAMETER NoCert         Skip signing.
.PARAMETER NoBuild        Skip dotnet publish; use existing publish output. Mutually exclusive with -Clean.
.PARAMETER Clean          Delete bin/ and obj/ before publishing. Mutually exclusive with -NoBuild.
.PARAMETER BumpMajor      Increment major in GitVersion.yml before building.
.PARAMETER BumpMinor      Increment minor in GitVersion.yml before building.
.PARAMETER BumpPatch      Increment patch in GitVersion.yml before building.
.PARAMETER Force          Skip the AppxManifest review pause.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [bool]   $SelfContained = $true,
    [switch] $Install,
    [switch] $NoCert,
    [switch] $NoBuild,
    [switch] $Clean,
    [switch] $BumpMajor,
    [switch] $BumpMinor,
    [switch] $BumpPatch,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$WorkspaceRoot = Convert-Path (Split-Path $PSScriptRoot -Parent)

# Locate module: prefer scripts/MsixTools submodule, fall back to sibling directory.
$ModulePath = Join-Path $PSScriptRoot 'MsixTools\MsixTools.psd1'
if (-not (Test-Path $ModulePath)) {
    $ModulePath = Join-Path (Split-Path $WorkspaceRoot -Parent) 'MsixTools\MsixTools.psd1'
}
if (-not (Test-Path $ModulePath)) { Write-Error 'MsixTools not found. Run: git submodule update --init' }
Import-Module $ModulePath -Force

$params = @{
    WorkspaceRoot = $WorkspaceRoot
    ConfigPath    = Join-Path $WorkspaceRoot 'msix.yml'
    Configuration = $Configuration
    SelfContained = $SelfContained
    Clean         = $Clean
    NoBuild       = $NoBuild
    BumpMajor     = $BumpMajor
    BumpMinor     = $BumpMinor
    BumpPatch     = $BumpPatch
    Force         = $Force
    Install       = $Install
}
if (-not $NoCert) { $params['DevCert'] = $true }

New-MsixPackage @params
