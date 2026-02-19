#Requires -Version 7.0
<#
.SYNOPSIS
  Build an MSIX package for RequestTracker Desktop using MsixTools.
.PARAMETER Configuration  Build configuration: Release (default) or Debug.
.PARAMETER NoBuild        Skip dotnet publish; use existing publish output.
.PARAMETER Clean          Delete bin/ and obj/ before publishing.
.PARAMETER Force          Skip AppxManifest review pause.
.PARAMETER Install        Install the MSIX after packaging. Requires Administrator.
.PARAMETER NoCert         Skip signing.
.PARAMETER Version        Override SemVer version string.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoBuild,
    [switch] $Clean,
    [switch] $Force,
    [switch] $Install,
    [switch] $NoCert,
    [string] $Version = ''
)

$ErrorActionPreference = 'Stop'
$WorkspaceRoot = Convert-Path (Split-Path $PSScriptRoot -Parent)

$ModulePath = Join-Path $PSScriptRoot 'MsixTools\MsixTools.psd1'
if (-not (Test-Path $ModulePath)) {
    Write-Error "MsixTools not found at $ModulePath. Run: git submodule update --init"
}
Import-Module $ModulePath -Force

$params = @{
    WorkspaceRoot  = $WorkspaceRoot
    ConfigPath     = Join-Path $WorkspaceRoot 'msix.yml'
    Configuration  = $Configuration
    SelfContained  = $true
    ExcludeService = $true
    Clean          = $Clean
    NoBuild        = $NoBuild
    Force          = $Force
    Install        = $Install
}
if (-not $NoCert) { $params['DevCert'] = $true }
if ($Version)     { $params['Version'] = $Version }

New-MsixPackage @params
