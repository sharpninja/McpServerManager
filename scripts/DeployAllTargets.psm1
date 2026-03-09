Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:RepoRoot = Convert-Path (Split-Path $PSScriptRoot -Parent)
$script:BuildScript = Join-Path $script:RepoRoot 'build.ps1'

function New-DeploymentResult {
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][ValidateSet('Success', 'Skipped', 'Failed', 'WhatIf')][string]$Status,
        [Parameter(Mandatory)][string]$Message
    )

    [PSCustomObject]@{
        Target  = $Target
        Status  = $Status
        Message = $Message
    }
}

function Resolve-SelectedTargets {
    param([Parameter(Mandatory)][string[]]$RequestedTargets)

    $allTargets = @('Director', 'WebUi', 'AndroidPhone', 'AndroidEmulator', 'DesktopMsix', 'DesktopDeb')
    if ($RequestedTargets -contains 'All') {
        return $allTargets
    }

    return @($allTargets | Where-Object { $RequestedTargets -contains $_ })
}

function Invoke-NukeBuild {
    param([Parameter(Mandatory)][string[]]$Arguments)

    if (-not (Test-Path -LiteralPath $script:BuildScript)) {
        throw "build.ps1 not found at '$script:BuildScript'."
    }

    $output = & $script:BuildScript @Arguments 2>&1
    [PSCustomObject]@{
        ExitCode = $LASTEXITCODE
        Output   = @($output | ForEach-Object { "$_" })
    }
}

function Invoke-NukeDeploymentSelection {
    param(
        [Parameter(Mandatory)][string]$TargetName,
        [Parameter(Mandatory)][string]$Selection,
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [string]$AndroidPhoneSerial = '',
        [string]$AndroidEmulatorSerial = '',
        [string]$WslDistro = '',
        [switch]$WhatIf
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('--target')
    $arguments.Add('DeployAll')
    $arguments.Add('--configuration')
    $arguments.Add($Configuration)
    $arguments.Add('--deploy-selection')
    $arguments.Add($Selection)

    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $arguments.Add('--package-version')
        $arguments.Add($PackageVersion)
    }
    if (-not [string]::IsNullOrWhiteSpace($AndroidPhoneSerial)) {
        $arguments.Add('--android-phone-serial')
        $arguments.Add($AndroidPhoneSerial)
    }
    if (-not [string]::IsNullOrWhiteSpace($AndroidEmulatorSerial)) {
        $arguments.Add('--android-emulator-serial')
        $arguments.Add($AndroidEmulatorSerial)
    }
    if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
        $arguments.Add('--wsl-distro')
        $arguments.Add($WslDistro)
    }
    if ($WhatIf) {
        $arguments.Add('--what-if')
    }

    $result = Invoke-NukeBuild -Arguments $arguments
    if ($result.ExitCode -ne 0) {
        $tail = @($result.Output | Select-Object -Last 10) -join [Environment]::NewLine
        return New-DeploymentResult -Target $TargetName -Status 'Failed' -Message $tail
    }

    foreach ($line in @($result.Output)) {
        $text = "$line"
        $match = [regex]::Match($text, "\b$([regex]::Escape($TargetName)):\s+(Success|Skipped|Failed|WhatIf)\s+-\s+(.*)$")
        if ($match.Success) {
            return New-DeploymentResult -Target $TargetName -Status $match.Groups[1].Value -Message $match.Groups[2].Value.Trim()
        }
    }

    $status = if ($WhatIf) { 'WhatIf' } else { 'Success' }
    $message = "Invoked NUKE deployment selection '$Selection'."
    return New-DeploymentResult -Target $TargetName -Status $status -Message $message
}

function Invoke-DeployDirectorTool {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'Director' -Selection 'Director' -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf
}

function Invoke-DeployWebUiTool {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'WebUi' -Selection 'WebUi' -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf
}

function Invoke-DeployAndroidPhone {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$DeviceSerial = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'AndroidPhone' -Selection 'AndroidPhone' -Configuration $Configuration -AndroidPhoneSerial $DeviceSerial -WhatIf:$WhatIf
}

function Invoke-DeployAndroidEmulator {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$DeviceSerial = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'AndroidEmulator' -Selection 'AndroidEmulator' -Configuration $Configuration -AndroidEmulatorSerial $DeviceSerial -WhatIf:$WhatIf
}

function Invoke-DeployDesktopMsix {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'DesktopMsix' -Selection 'DesktopMsix' -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf
}

function Invoke-DeployDesktopDeb {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [string]$WslDistro = '',
        [switch]$WhatIf
    )

    Invoke-NukeDeploymentSelection -TargetName 'DesktopDeb' -Selection 'DesktopDeb' -Configuration $Configuration -PackageVersion $PackageVersion -WslDistro $WslDistro -WhatIf:$WhatIf
}

function Invoke-DeployTargetSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]]$Targets,
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
        [string]$PackageVersion = '',
        [string]$AndroidPhoneSerial = '',
        [string]$AndroidEmulatorSerial = '',
        [string]$WslDistro = '',
        [switch]$WhatIf
    )

    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($target in @(Resolve-SelectedTargets -RequestedTargets $Targets)) {
        switch ($target) {
            'Director' { $results.Add((Invoke-DeployDirectorTool -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf)) }
            'WebUi' { $results.Add((Invoke-DeployWebUiTool -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf)) }
            'AndroidPhone' { $results.Add((Invoke-DeployAndroidPhone -Configuration $Configuration -DeviceSerial $AndroidPhoneSerial -WhatIf:$WhatIf)) }
            'AndroidEmulator' { $results.Add((Invoke-DeployAndroidEmulator -Configuration $Configuration -DeviceSerial $AndroidEmulatorSerial -WhatIf:$WhatIf)) }
            'DesktopMsix' { $results.Add((Invoke-DeployDesktopMsix -Configuration $Configuration -PackageVersion $PackageVersion -WhatIf:$WhatIf)) }
            'DesktopDeb' { $results.Add((Invoke-DeployDesktopDeb -Configuration $Configuration -PackageVersion $PackageVersion -WslDistro $WslDistro -WhatIf:$WhatIf)) }
        }
    }

    return @($results)
}

function Show-DeploySummary {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Results)

    Write-Host 'Deployment summary' -ForegroundColor Cyan
    foreach ($result in @($Results)) {
        Write-Host ("{0}: {1} - {2}" -f $result.Target, $result.Status, $result.Message)
    }

    $successCount = @($Results | Where-Object Status -eq 'Success').Count
    $whatIfCount = @($Results | Where-Object Status -eq 'WhatIf').Count
    $skippedCount = @($Results | Where-Object Status -eq 'Skipped').Count
    $failedCount = @($Results | Where-Object Status -eq 'Failed').Count
    Write-Host ("Success={0}  WhatIf={1}  Skipped={2}  Failed={3}" -f $successCount, $whatIfCount, $skippedCount, $failedCount)
}

Export-ModuleMember -Function @(
    'Invoke-DeployDirectorTool',
    'Invoke-DeployWebUiTool',
    'Invoke-DeployAndroidPhone',
    'Invoke-DeployAndroidEmulator',
    'Invoke-DeployDesktopMsix',
    'Invoke-DeployDesktopDeb',
    'Invoke-DeployTargetSet',
    'Show-DeploySummary'
)
