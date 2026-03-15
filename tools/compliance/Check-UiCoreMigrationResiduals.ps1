param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
)

$ErrorActionPreference = "Stop"

$findings = [System.Collections.Generic.List[object]]::new()

function Add-Finding {
    param(
        [string]$Rule,
        [string]$File,
        [string]$Message
    )

    $findings.Add([pscustomobject]@{
            Rule = $Rule
            File = $File
            Message = $Message
    })
}

function Resolve-RepoPath {
    param([string]$RelativePath)

    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($RelativePath)

    if ($RelativePath -match '^src[\\/](.+)$') {
        $candidates.Add("lib/McpServer/src/$($matches[1])")
    }

    if ($RelativePath -match '^lib[\\/]McpServer[\\/]src[\\/](.+)$') {
        $candidates.Add("src/$($matches[1])")
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        $path = Join-Path $RepoRoot $candidate
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    return Join-Path $RepoRoot $RelativePath
}

function Assert-NoRegexMatches {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$FileFilter,
        [string]$Pattern,
        [string]$Message
    )

    $fullPath = Resolve-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        Add-Finding -Rule $Rule -File $RelativePath -Message "Required path not found."
        return
    }

    $matches = Get-ChildItem -LiteralPath $fullPath -Recurse -File -Filter $FileFilter |
        Select-String -Pattern $Pattern

    if ($matches) {
        $first = $matches | Select-Object -First 1
        Add-Finding -Rule $Rule -File ([IO.Path]::GetRelativePath($RepoRoot, $first.Path).Replace("\", "/")) -Message "$Message (line $($first.LineNumber))."
    }
}

$inventoryScript = Join-Path $PSScriptRoot "Get-UiCoreMigrationInventory.ps1"
if (-not (Test-Path -LiteralPath $inventoryScript)) {
    Add-Finding -Rule "RSD001" -File "tools/compliance/Get-UiCoreMigrationInventory.ps1" -Message "Required inventory script not found."
}
else {
    try {
        $inventoryJson = & $inventoryScript -RepoRoot $RepoRoot
        $inventory = $inventoryJson | ConvertFrom-Json
        if ($inventory.HostIsolation.DirectorLocalViewModelCount -ne 0) {
            Add-Finding -Rule "RSD001" -File "src/McpServer.Director" -Message "Legacy Director-local ViewModel declarations remain."
        }

        if ($inventory.HostIsolation.WebLocalViewModelCount -ne 0) {
            Add-Finding -Rule "RSD001" -File "src/McpServer.Web" -Message "Legacy Web-local ViewModel declarations remain."
        }
    }
    catch {
        Add-Finding -Rule "RSD001" -File "tools/compliance/Get-UiCoreMigrationInventory.ps1" -Message "Failed to run inventory script: $($_.Exception.Message)"
    }
}

$legacyShimPath = Join-Path $RepoRoot "src/McpServerManager.Core/Commands/InvokeUiActionCommand.cs"
if (Test-Path -LiteralPath $legacyShimPath) {
    Add-Finding -Rule "RSD002" -File "src/McpServerManager.Core/Commands/InvokeUiActionCommand.cs" -Message "Legacy InvokeUiActionCommand shim should be removed after shared relay migration."
}

Assert-NoRegexMatches `
    -Rule "RSD003" `
    -RelativePath "src/McpServerManager.Core" `
    -FileFilter "*.cs" `
    -Pattern "McpServerManager\.Core\.Commands\.InvokeUiActionCommand|new\s+McpServerManager\.Core\.Commands\.InvokeUiActionCommand\s*\(" `
    -Message "Legacy host InvokeUiActionCommand references remain."

Assert-NoRegexMatches `
    -Rule "RSD004" `
    -RelativePath "src/McpServerManager.Core/ViewModels" `
    -FileFilter "*Commands.cs" `
    -Pattern "new\s+CqrsRelayCommand<bool>\s*\(" `
    -Message "Host ViewModel command files should use relay factory helpers instead of direct CqrsRelayCommand construction."

Assert-NoRegexMatches `
    -Rule "RSD005" `
    -RelativePath "src/McpServerManager.Core" `
    -FileFilter "*.cs" `
    -Pattern "ConfigureAwait\(false\)" `
    -Message "ConfigureAwait(false) is not allowed in McpServerManager.Core migration scope."

Assert-NoRegexMatches `
    -Rule "RSD006" `
    -RelativePath "src/McpServer.UI.Core" `
    -FileFilter "*.cs" `
    -Pattern "ConfigureAwait\(false\)" `
    -Message "ConfigureAwait(false) is not allowed in McpServer.UI.Core migration scope."

Assert-NoRegexMatches `
    -Rule "RSD007" `
    -RelativePath "src" `
    -FileFilter "*.cs" `
    -Pattern '"/mcp/|"mcp/' `
    -Message "Legacy /mcp/ route prefix references remain in src/."

if ($findings.Count -eq 0) {
    Write-Host "UI.Core migration residual cleanup check passed."
    exit 0
}

Write-Host ("UI.Core migration residual cleanup check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 260 |
    Write-Host

exit 1
