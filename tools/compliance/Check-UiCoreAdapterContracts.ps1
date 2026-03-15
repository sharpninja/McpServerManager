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
        File = "src/McpServer.Director/TodoApiClientAdapter.cs"
        Pattern = "class\s+TodoApiClientAdapter\s*:\s*ITodoApiClient\b"
        Message = "Director TODO adapter must implement ITodoApiClient."
    },
    @{
        Rule = "ADP002"
        File = "src/McpServer.Director/WorkspaceApiClientAdapter.cs"
        Pattern = "class\s+WorkspaceApiClientAdapter\s*:\s*IWorkspaceApiClient\b"
        Message = "Director workspace adapter must implement IWorkspaceApiClient."
    },
    @{
        Rule = "ADP003"
        File = "src/McpServer.Director/SessionLogApiClientAdapter.cs"
        Pattern = "class\s+SessionLogApiClientAdapter\s*:\s*ISessionLogApiClient\b"
        Message = "Director session log adapter must implement ISessionLogApiClient."
    },
    @{
        Rule = "ADP004"
        File = "src/McpServer.Web/Adapters/TodoApiClientAdapter.cs"
        Pattern = "class\s+TodoApiClientAdapter\s*:\s*ITodoApiClient\b"
        Message = "Web TODO adapter must implement ITodoApiClient."
    },
    @{
        Rule = "ADP005"
        File = "src/McpServer.Web/Adapters/WorkspaceApiClientAdapter.cs"
        Pattern = "class\s+WorkspaceApiClientAdapter\s*:\s*IWorkspaceApiClient\b"
        Message = "Web workspace adapter must implement IWorkspaceApiClient."
    },
    @{
        Rule = "ADP006"
        File = "src/McpServer.Web/Adapters/SessionLogApiClientAdapter.cs"
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
        File = "src/McpServer.Director/DirectorServiceRegistration.cs"
        Pattern = "AddSingleton<ITodoApiClient>\s*\(_\s*=>\s*new\s+TodoApiClientAdapter\("
        Message = "Director service registration must wire ITodoApiClient to TodoApiClientAdapter."
    },
    @{
        Rule = "ADP102"
        File = "src/McpServer.Director/DirectorServiceRegistration.cs"
        Pattern = "AddSingleton<IWorkspaceApiClient>\s*\(_\s*=>\s*new\s+WorkspaceApiClientAdapter\("
        Message = "Director service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter."
    },
    @{
        Rule = "ADP103"
        File = "src/McpServer.Director/DirectorServiceRegistration.cs"
        Pattern = "AddSingleton<ISessionLogApiClient>\s*\(_\s*=>\s*new\s+SessionLogApiClientAdapter\("
        Message = "Director service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter."
    },
    @{
        Rule = "ADP104"
        File = "src/McpServer.Web/WebServiceRegistration.cs"
        Pattern = "AddScoped<ITodoApiClient,\s*TodoApiClientAdapter>\s*\(\s*\)"
        Message = "Web service registration must wire ITodoApiClient to TodoApiClientAdapter."
    },
    @{
        Rule = "ADP105"
        File = "src/McpServer.Web/WebServiceRegistration.cs"
        Pattern = "AddScoped<IWorkspaceApiClient,\s*WorkspaceApiClientAdapter>\s*\(\s*\)"
        Message = "Web service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter."
    },
    @{
        Rule = "ADP106"
        File = "src/McpServer.Web/WebServiceRegistration.cs"
        Pattern = "AddScoped<ISessionLogApiClient,\s*SessionLogApiClientAdapter>\s*\(\s*\)"
        Message = "Web service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter."
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
