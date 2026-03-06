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

function Get-ContentOrFinding {
    param(
        [string]$Rule,
        [string]$RelativePath
    )

    $fullPath = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        Add-Finding -Rule $Rule -File $RelativePath -Message "Required file not found."
        return $null
    }

    return Get-Content -LiteralPath $fullPath -Raw
}

function Assert-PatternPresent {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$Pattern,
        [string]$Message
    )

    $content = Get-ContentOrFinding -Rule $Rule -RelativePath $RelativePath
    if ($null -eq $content) {
        return
    }

    if ($content -notmatch $Pattern) {
        Add-Finding -Rule $Rule -File $RelativePath -Message $Message
    }
}

function Assert-PatternAbsent {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$Pattern,
        [string]$Message
    )

    $content = Get-ContentOrFinding -Rule $Rule -RelativePath $RelativePath
    if ($null -eq $content) {
        return
    }

    if ($content -match $Pattern) {
        Add-Finding -Rule $Rule -File $RelativePath -Message $Message
    }
}

Assert-PatternPresent `
    -Rule "SHR001" `
    -RelativePath "src/McpServerManager.Core/ViewModels/TodoListViewModel.cs" `
    -Pattern "class\s+TodoListViewModel\s*:\s*McpServer\.UI\.Core\.ViewModels\.TodoListHostViewModel\b" `
    -Message "Todo host wrapper must inherit McpServer.UI.Core.ViewModels.TodoListHostViewModel."

Assert-PatternAbsent `
    -Rule "SHR002" `
    -RelativePath "src/McpServerManager.Core/ViewModels/TodoListViewModel.cs" `
    -Pattern "\bMcpTodoService\b|\bMcpServerClient\b|\bQueryAsync\s*\(" `
    -Message "Todo host wrapper must not contain direct MCP service/client orchestration."

Assert-PatternPresent `
    -Rule "SHR003" `
    -RelativePath "src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs" `
    -Pattern "class\s+WorkspaceViewModel\s*:\s*McpServer\.UI\.Core\.ViewModels\.WorkspaceViewModel\b" `
    -Message "Workspace host wrapper must inherit McpServer.UI.Core.ViewModels.WorkspaceViewModel."

Assert-PatternAbsent `
    -Rule "SHR004" `
    -RelativePath "src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs" `
    -Pattern "\bMcpWorkspaceService\b|\bMcpServerClient\b|\bGetStatusAsync\s*\(" `
    -Message "Workspace host wrapper must not contain direct workspace service orchestration."

Assert-PatternPresent `
    -Rule "SHR005" `
    -RelativePath "src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs" `
    -Pattern "class\s+MainWindowViewModel\s*:\s*McpServer\.UI\.Core\.ViewModels\.MainWindowViewModel\b" `
    -Message "MainWindow host wrapper must inherit McpServer.UI.Core.ViewModels.MainWindowViewModel."

Assert-PatternAbsent `
    -Rule "SHR006" `
    -RelativePath "src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs" `
    -Pattern "LoadTodosCoreAsync\s*\(|LoadWorkspacesCoreAsync\s*\(|ReloadFromMcpAsyncInternal\s*\(|LoadJsonCoreAsync\s*\(" `
    -Message "MainWindow host wrapper must not reintroduce shared orchestration methods."

if ($findings.Count -eq 0) {
    Write-Host "UI.Core shared orchestration check passed."
    exit 0
}

Write-Host ("UI.Core shared orchestration check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
