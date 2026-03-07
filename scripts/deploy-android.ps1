#Requires -Version 5.1
<#
.SYNOPSIS
  Builds and deploys the McpServerManager Android app to the attached device.

.PARAMETER Configuration
  Build configuration: Debug (default) or Release.

.PARAMETER DeviceSerial
  ADB device serial. Defaults to ZD222QH58Q (Motorola Edge).

.EXAMPLE
  .\deploy-android.ps1
  .\deploy-android.ps1 -Configuration Release
  .\deploy-android.ps1 -DeviceSerial emulator-5554
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$DeviceSerial = "ZD222QH58Q"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\McpServerManager.Android\McpServerManager.Android.csproj"

# Resolve TargetFramework from the Android project (fallback keeps legacy behavior).
[xml]$projectXml = Get-Content $projectPath
$targetFramework = $null
if ($projectXml.Project -and $projectXml.Project.PropertyGroup) {
    foreach ($pg in $projectXml.Project.PropertyGroup) {
        if ($pg.TargetFramework) {
            $candidate = [string]$pg.TargetFramework
            if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                $targetFramework = $candidate.Trim()
                break
            }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    $targetFramework = "net9.0-android"
}

# Ensure adb is available
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) {
    Write-Error "adb not found in PATH. Install Android SDK platform-tools."
}

# Check device is attached
$deviceLines = adb devices | Select-String "device$"
if (-not $deviceLines -or $deviceLines.Count -eq 0) {
    Write-Error "No Android device/emulator connected. Enable USB debugging and run: adb devices"
}
Write-Host "Device(s) attached:" -ForegroundColor Cyan
adb devices -l

Write-Host "`n[1/2] Building and installing ($Configuration, $targetFramework) to $DeviceSerial..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    dotnet build $projectPath -t:Install -f $targetFramework -c $Configuration -p:AdbTarget="-s $DeviceSerial"
    if ($LASTEXITCODE -ne 0) { throw "Build/Install failed" }
} finally {
    Pop-Location
}

Write-Host "`n[2/2] Done. McpServerManager installed on $DeviceSerial." -ForegroundColor Green
