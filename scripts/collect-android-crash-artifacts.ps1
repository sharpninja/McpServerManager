#Requires -Version 5.1
<#
.SYNOPSIS
  Prepares or collects Android crash artifacts for the RequestTracker phone app.

.DESCRIPTION
  Use `-Phase Prepare` before reproducing the crash to clear logcat and create an
  output folder. Use `-Phase Collect` after the crash to capture logcat, process
  exit info, app diagnostics files, and optional bugreport output.

.EXAMPLE
  .\collect-android-crash-artifacts.ps1 -Phase Prepare
  .\collect-android-crash-artifacts.ps1 -Phase Collect -OutputRoot .\artifacts\android-crash\20260303-140000
#>
param(
    [ValidateSet("Prepare", "Collect")]
    [string]$Phase = "Collect",

    [string]$DeviceSerial = "ZD222QH58Q",

    [string]$PackageName = "ninja.thesharp.mcpservermanager",

    [string]$OutputRoot,

    [switch]$IncludeBugreport
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot ("artifacts\android-crash\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

$null = New-Item -ItemType Directory -Force -Path $OutputRoot

$adbCommand = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adbCommand) {
    throw "adb not found in PATH. Install Android SDK platform-tools."
}

$adb = $adbCommand.Source

function Invoke-AdbCapture {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $output = & $adb @Arguments 2>&1 | Out-String
    if (-not $AllowFailure -and $LASTEXITCODE -ne 0) {
        throw "adb $($Arguments -join ' ') failed.`n$output"
    }

    return $output
}

function Write-ArtifactFile {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [AllowEmptyString()]
        [AllowNull()]
        [string]$Content
    )

    $path = Join-Path $OutputRoot $Name
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        $null = New-Item -ItemType Directory -Force -Path $directory
    }

    if ($null -eq $Content) {
        $Content = ""
    }

    Set-Content -Path $path -Value $Content -Encoding UTF8
}

function Test-DeviceAttached {
    $devices = Invoke-AdbCapture -Arguments @("devices") -AllowFailure
    $match = $devices -split "`r?`n" | Where-Object { $_ -match "^$([regex]::Escape($DeviceSerial))\s+device$" }
    if (-not $match) {
        throw "Device '$DeviceSerial' is not attached. Run `adb devices` and verify USB debugging."
    }
}

function Write-SessionMetadata {
    $metadata = [PSCustomObject]@{
        phase = $Phase
        deviceSerial = $DeviceSerial
        packageName = $PackageName
        outputRoot = (Resolve-Path $OutputRoot).Path
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json -Depth 4

    Write-ArtifactFile -Name "session-metadata.json" -Content $metadata
}

function Write-CommonArtifacts {
    Write-ArtifactFile -Name "adb-devices.txt" -Content (Invoke-AdbCapture -Arguments @("devices", "-l") -AllowFailure)
    Write-ArtifactFile -Name "device-getprop.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "getprop") -AllowFailure)
    Write-ArtifactFile -Name "device-build.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "dumpsys", "package", $PackageName) -AllowFailure)
}

function Export-AppDiagnosticsTextArtifacts {
    $listing = Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "run-as", $PackageName, "sh", "-c", "ls -1 files/diagnostics/crash 2>/dev/null") -AllowFailure
    Write-ArtifactFile -Name "app-diagnostics-listing.txt" -Content $listing

    if ($LASTEXITCODE -ne 0) {
        return
    }

    $names = $listing -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    foreach ($name in $names) {
        $targetName = "app-diagnostics\" + $name.Replace("/", "_")
        if ($name -match "\.(json|txt|log)$") {
            $content = Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "exec-out", "run-as", $PackageName, "cat", ("files/diagnostics/crash/" + $name)) -AllowFailure
            Write-ArtifactFile -Name $targetName -Content $content
            continue
        }

        Write-ArtifactFile -Name ($targetName + ".note.txt") -Content "Skipped non-text artifact '$name'. Use -IncludeBugreport for deeper native/ANR evidence."
    }
}

function Write-PrepareInstructions {
    $instructions = @"
Prepared Android crash capture workspace at:
$OutputRoot

Next steps:
1. Reproduce the crash on device $DeviceSerial.
2. Relaunch the app once after the crash so recovered crash diagnostics can be replayed into the app log.
3. Run:
   .\scripts\collect-android-crash-artifacts.ps1 -Phase Collect -DeviceSerial $DeviceSerial -OutputRoot `"$OutputRoot`"

Optional:
- Add -IncludeBugreport if you suspect a native crash or ANR and need a full system bugreport.
"@

    Write-ArtifactFile -Name "README.txt" -Content $instructions
}

Test-DeviceAttached
Write-SessionMetadata
Write-CommonArtifacts

if ($Phase -eq "Prepare") {
    Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "wait-for-device") | Out-Null
    Write-ArtifactFile -Name "logcat-clear.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "logcat", "-b", "all", "-c") -AllowFailure)
    Write-ArtifactFile -Name "package-pid-before.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "pidof", $PackageName) -AllowFailure)
    Write-PrepareInstructions
    Write-Host "Prepared crash capture workspace at $OutputRoot" -ForegroundColor Green
    exit 0
}

Write-ArtifactFile -Name "logcat.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "logcat", "-d", "-b", "all", "-v", "threadtime") -AllowFailure)
Write-ArtifactFile -Name "package-pid-after.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "pidof", $PackageName) -AllowFailure)
Write-ArtifactFile -Name "meminfo.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "dumpsys", "meminfo", $PackageName) -AllowFailure)
Write-ArtifactFile -Name "activity-exit-info.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "dumpsys", "activity", "exit-info", $PackageName) -AllowFailure)
Write-ArtifactFile -Name "tombstones-access.txt" -Content (Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "shell", "ls", "-al", "/data/tombstones") -AllowFailure)
Export-AppDiagnosticsTextArtifacts

if ($IncludeBugreport) {
    $bugreportBase = Join-Path $OutputRoot "bugreport"
    $bugreportOutput = Invoke-AdbCapture -Arguments @("-s", $DeviceSerial, "bugreport", $bugreportBase) -AllowFailure
    Write-ArtifactFile -Name "bugreport-command-output.txt" -Content $bugreportOutput
}

Write-Host "Collected Android crash artifacts under $OutputRoot" -ForegroundColor Green
