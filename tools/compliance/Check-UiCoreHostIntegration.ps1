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

# Web composition root invariants
Assert-ContainsPattern `
    -Rule "HIN001" `
    -RelativePath "src/McpServerManager.Web/WebServiceRegistration.cs" `
    -Pattern "AddMcpHost\(options\s*=>" `
    -MissingMessage "Web service registration must use AddMcpHost for shared ViewModel/handler wiring."

Assert-ContainsPattern `
    -Rule "HIN001B" `
    -RelativePath "src/McpServerManager.Web/WebServiceRegistration.cs" `
    -Pattern "AdditionalHandlerAssemblies\s*=\s*\[\s*typeof\(WebServiceRegistration\)\.Assembly\s*\]" `
    -MissingMessage "Web service registration must include the Web assembly in AddMcpHost handler discovery."

Assert-ContainsPattern `
    -Rule "HIN002" `
    -RelativePath "src/McpServerManager.Web/WebServiceRegistration.cs" `
    -Pattern "TodoClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<TodoApiClientAdapter>\(sp\)" `
    -MissingMessage "Web service registration must wire ITodoApiClient to TodoApiClientAdapter through AddMcpHost."

Assert-ContainsPattern `
    -Rule "HIN003" `
    -RelativePath "src/McpServerManager.Web/WebServiceRegistration.cs" `
    -Pattern "WorkspaceClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<WorkspaceApiClientAdapter>\(sp\)" `
    -MissingMessage "Web service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter through AddMcpHost."

Assert-ContainsPattern `
    -Rule "HIN004" `
    -RelativePath "src/McpServerManager.Web/WebServiceRegistration.cs" `
    -Pattern "SessionLogClientFactory\s*=\s*static\s+sp\s*=>\s*ActivatorUtilities\.CreateInstance<SessionLogApiClientAdapter>\(sp\)" `
    -MissingMessage "Web service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter through AddMcpHost."

# Director composition root invariants
Assert-ContainsPattern `
    -Rule "HIN101" `
    -RelativePath "src/McpServerManager.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddMcpHost\(options\s*=>" `
    -MissingMessage "Director DI registration must use AddMcpHost for shared UI.Core and CQRS wiring."

Assert-ContainsPattern `
    -Rule "HIN102" `
    -RelativePath "src/McpServerManager.Director/DirectorServiceRegistration.cs" `
    -Pattern "options\.Lifetime\s*=\s*McpHostLifetimeStrategy\.Singleton" `
    -MissingMessage "Director DI registration must use singleton AddMcpHost lifetime."

Assert-ContainsPattern `
    -Rule "HIN103" `
    -RelativePath "src/McpServerManager.Director/DirectorServiceRegistration.cs" `
    -Pattern "options\.TodoClient\s*=\s*new\s+TodoApiClientAdapter\(directorContext\)" `
    -MissingMessage "Director DI registration must wire ITodoApiClient to TodoApiClientAdapter through AddMcpHost."

Assert-ContainsPattern `
    -Rule "HIN104" `
    -RelativePath "src/McpServerManager.Director/DirectorServiceRegistration.cs" `
    -Pattern "options\.WorkspaceClient\s*=\s*new\s+WorkspaceApiClientAdapter\(directorContext\)" `
    -MissingMessage "Director DI registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter through AddMcpHost."

Assert-ContainsPattern `
    -Rule "HIN105" `
    -RelativePath "src/McpServerManager.Director/DirectorServiceRegistration.cs" `
    -Pattern "options\.SessionLogClient\s*=\s*new\s+SessionLogApiClientAdapter\(directorContext\)" `
    -MissingMessage "Director DI registration must wire ISessionLogApiClient to SessionLogApiClientAdapter through AddMcpHost."

if ($findings.Count -eq 0) {
    Write-Host "UI.Core host integration check passed."
    exit 0
}

Write-Host ("UI.Core host integration check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
