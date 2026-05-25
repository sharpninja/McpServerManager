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

function Assert-ContainsPattern {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$Pattern,
        [string]$MissingMessage
    )

    $fullPath = Resolve-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        Add-Finding -Rule $Rule -File $RelativePath -Message "Required file not found."
        return
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -notmatch $Pattern) {
        Add-Finding -Rule $Rule -File $RelativePath -Message $MissingMessage
    }
}

$adapterContracts = @(
    @{
        Rule = "ADP001"
        File = "src/McpServerManager.Director/TodoApiClientAdapter.cs"
        Pattern = "class\s+TodoApiClientAdapter\s*:\s*ITodoApiClient\b"
        Message = "Director TODO adapter must implement ITodoApiClient."
    },
    @{
        Rule = "ADP002"
        File = "src/McpServerManager.Director/WorkspaceApiClientAdapter.cs"
        Pattern = "class\s+WorkspaceApiClientAdapter\s*:\s*IWorkspaceApiClient\b"
        Message = "Director workspace adapter must implement IWorkspaceApiClient."
    },
    @{
        Rule = "ADP003"
        File = "src/McpServerManager.Director/SessionLogApiClientAdapter.cs"
        Pattern = "class\s+SessionLogApiClientAdapter\s*:\s*ISessionLogApiClient\b"
        Message = "Director session log adapter must implement ISessionLogApiClient."
    },
    @{
        Rule = "ADP004"
        File = "src/McpServerManager.Web/Adapters/TodoApiClientAdapter.cs"
        Pattern = "class\s+TodoApiClientAdapter\s*:\s*ITodoApiClient\b"
        Message = "Web TODO adapter must implement ITodoApiClient."
    },
    @{
        Rule = "ADP005"
        File = "src/McpServerManager.Web/Adapters/WorkspaceApiClientAdapter.cs"
        Pattern = "class\s+WorkspaceApiClientAdapter\s*:\s*IWorkspaceApiClient\b"
        Message = "Web workspace adapter must implement IWorkspaceApiClient."
    },
    @{
        Rule = "ADP006"
        File = "src/McpServerManager.Web/Adapters/SessionLogApiClientAdapter.cs"
        Pattern = "class\s+SessionLogApiClientAdapter\s*:\s*ISessionLogApiClient\b"
        Message = "Web session log adapter must implement ISessionLogApiClient."
    }
)

foreach ($contract in $adapterContracts) {
    Assert-ContainsPattern `
        -Rule $contract.Rule `
        -RelativePath $contract.File `
        -Pattern $contract.Pattern `
        -MissingMessage $contract.Message
}

$registrationChecks = @(
    @{
        Rule = "ADP101"
        File = "src/McpServerManager.Director/DirectorServiceRegistration.cs"
        Pattern = "options\.TodoClient\s*=\s*new\s+TodoApiClientAdapter\(directorContext\)"
        Message = "Director service registration must wire ITodoApiClient to TodoApiClientAdapter through AddMcpHost."
    },
    @{
        Rule = "ADP102"
        File = "src/McpServerManager.Director/DirectorServiceRegistration.cs"
        Pattern = "options\.WorkspaceClient\s*=\s*new\s+WorkspaceApiClientAdapter\(directorContext\)"
        Message = "Director service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter through AddMcpHost."
    },
    @{
        Rule = "ADP103"
        File = "src/McpServerManager.Director/DirectorServiceRegistration.cs"
        Pattern = "options\.SessionLogClient\s*=\s*new\s+SessionLogApiClientAdapter\(directorContext\)"
        Message = "Director service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter through AddMcpHost."
    },
    @{
        Rule = "ADP104"
        File = "src/McpServerManager.Web/WebServiceRegistration.cs"
        Pattern = "TodoClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<TodoApiClientAdapter>\(sp\)"
        Message = "Web service registration must wire ITodoApiClient to TodoApiClientAdapter through AddMcpHost."
    },
    @{
        Rule = "ADP105"
        File = "src/McpServerManager.Web/WebServiceRegistration.cs"
        Pattern = "WorkspaceClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<WorkspaceApiClientAdapter>\(sp\)"
        Message = "Web service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter through AddMcpHost."
    },
    @{
        Rule = "ADP106"
        File = "src/McpServerManager.Web/WebServiceRegistration.cs"
        Pattern = "SessionLogClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<SessionLogApiClientAdapter>\(sp\)"
        Message = "Web service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter through AddMcpHost."
    }
)

foreach ($check in $registrationChecks) {
    Assert-ContainsPattern `
        -Rule $check.Rule `
        -RelativePath $check.File `
        -Pattern $check.Pattern `
        -MissingMessage $check.Message
}

if ($findings.Count -eq 0) {
    Write-Host "UI.Core adapter contract check passed."
    exit 0
}

Write-Host ("UI.Core adapter contract check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
