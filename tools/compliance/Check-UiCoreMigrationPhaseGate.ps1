param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
)

$ErrorActionPreference = "Stop"

$findings = [System.Collections.Generic.List[object]]::new()

function Add-Finding {
    param(
        [string]$Rule,
        [string]$Message
    )

    $findings.Add([pscustomobject]@{
            Rule = $Rule
            Message = $Message
        })
}

$requiredArtifacts = @(
    "docs/architecture/compliance/MIGRATION-DECISION-LOG.template.yaml",
    "docs/architecture/compliance/MIGRATION-PARITY-CHECKLIST.template.yaml",
    "tools/compliance/Get-UiCoreMigrationInventory.ps1"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $RepoRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Finding -Rule "PG001" -Message "Required migration artifact missing: $artifact"
    }
}

$inventoryPath = Join-Path $RepoRoot "tools/compliance/Get-UiCoreMigrationInventory.ps1"
$inventory = $null
if (Test-Path -LiteralPath $inventoryPath) {
    try {
        $inventoryJson = & $inventoryPath -RepoRoot $RepoRoot
        $inventory = $inventoryJson | ConvertFrom-Json
    }
    catch {
        Add-Finding -Rule "PG002" -Message "Failed to execute or parse migration inventory output: $($_.Exception.Message)"
    }
}

if ($null -ne $inventory) {
    if (($inventory.ManagerCore.CommandCount -lt 1) -or ($inventory.ManagerCore.HandlerCount -lt 1)) {
        Add-Finding -Rule "PG003" -Message "ManagerCore command/handler inventory is empty or incomplete."
    }

    if (($inventory.UiCoreMessages.CommandCount -lt 1) -or ($inventory.UiCoreMessages.QueryCount -lt 1)) {
        Add-Finding -Rule "PG004" -Message "UiCoreMessages command/query inventory is empty or incomplete."
    }

    if ($inventory.HostWrappers.Count -lt 1) {
        Add-Finding -Rule "PG005" -Message "Host wrapper inventory is empty."
    }

    if ($inventory.HostIsolation.DirectorLocalViewModelCount -ne 0) {
        Add-Finding -Rule "PG006" -Message "Director host declares local *ViewModel classes ($($inventory.HostIsolation.DirectorLocalViewModelCount))."
    }

    if ($inventory.HostIsolation.WebLocalViewModelCount -ne 0) {
        Add-Finding -Rule "PG007" -Message "Web host declares local *ViewModel classes ($($inventory.HostIsolation.WebLocalViewModelCount))."
    }
}

if ($findings.Count -eq 0) {
    Write-Host "UI.Core migration phase gate passed."
    exit 0
}

Write-Error ("UI.Core migration phase gate failed with {0} finding(s)." -f $findings.Count)
$findings |
    Format-Table Rule, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
