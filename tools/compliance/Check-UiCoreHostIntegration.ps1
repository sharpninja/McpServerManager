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

function Assert-ContainsPattern {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$Pattern,
        [string]$MissingMessage
    )

    $fullPath = Join-Path $RepoRoot $RelativePath
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
    -RelativePath "lib/McpServer/src/McpServer.Web/WebServiceRegistration.cs" `
    -Pattern "AddUiCore\(typeof\(WebServiceRegistration\)\.Assembly\)" `
    -MissingMessage "Web service registration must call AddUiCore(typeof(WebServiceRegistration).Assembly) for shared ViewModel/handler wiring."

Assert-ContainsPattern `
    -Rule "HIN002" `
    -RelativePath "lib/McpServer/src/McpServer.Web/WebServiceRegistration.cs" `
    -Pattern "AddScoped<ITodoApiClient,\s*TodoApiClientAdapter>\s*\(\s*\)" `
    -MissingMessage "Web service registration must wire ITodoApiClient to TodoApiClientAdapter."

Assert-ContainsPattern `
    -Rule "HIN003" `
    -RelativePath "lib/McpServer/src/McpServer.Web/WebServiceRegistration.cs" `
    -Pattern "AddScoped<IWorkspaceApiClient,\s*WorkspaceApiClientAdapter>\s*\(\s*\)" `
    -MissingMessage "Web service registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter."

Assert-ContainsPattern `
    -Rule "HIN004" `
    -RelativePath "lib/McpServer/src/McpServer.Web/WebServiceRegistration.cs" `
    -Pattern "AddScoped<ISessionLogApiClient,\s*SessionLogApiClientAdapter>\s*\(\s*\)" `
    -MissingMessage "Web service registration must wire ISessionLogApiClient to SessionLogApiClientAdapter."

# Director composition root invariants
Assert-ContainsPattern `
    -Rule "HIN101" `
    -RelativePath "lib/McpServer/src/McpServer.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddCqrs\(typeof\(Program\)\.Assembly\)" `
    -MissingMessage "Director DI registration must initialize CQRS pipeline from Program assembly."

Assert-ContainsPattern `
    -Rule "HIN102" `
    -RelativePath "lib/McpServer/src/McpServer.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddUiCore\(\)" `
    -MissingMessage "Director DI registration must wire shared UI.Core services."

Assert-ContainsPattern `
    -Rule "HIN103" `
    -RelativePath "lib/McpServer/src/McpServer.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddSingleton<ITodoApiClient>\s*\(_\s*=>\s*new\s+TodoApiClientAdapter\(" `
    -MissingMessage "Director DI registration must wire ITodoApiClient to TodoApiClientAdapter."

Assert-ContainsPattern `
    -Rule "HIN104" `
    -RelativePath "lib/McpServer/src/McpServer.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddSingleton<IWorkspaceApiClient>\s*\(_\s*=>\s*new\s+WorkspaceApiClientAdapter\(" `
    -MissingMessage "Director DI registration must wire IWorkspaceApiClient to WorkspaceApiClientAdapter."

Assert-ContainsPattern `
    -Rule "HIN105" `
    -RelativePath "lib/McpServer/src/McpServer.Director/DirectorServiceRegistration.cs" `
    -Pattern "AddSingleton<ISessionLogApiClient>\s*\(_\s*=>\s*new\s+SessionLogApiClientAdapter\(" `
    -MissingMessage "Director DI registration must wire ISessionLogApiClient to SessionLogApiClientAdapter."

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
