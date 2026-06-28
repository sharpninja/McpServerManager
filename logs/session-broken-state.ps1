Import-Module .\McpSession.psm1
Initialize-McpSession
$ts = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$s = [PSCustomObject]@{
    sourceType = "GrokCode"
    sessionId = "GrokCode-20260627T021310Z-remediate-viewmodel-logic-cqrs"
    title = "Remediate all ViewModel logic code not in CQRS issues"
    model = "grok-4.3-2026"
    started = (Get-Date).ToUniversalTime().ToString("o")
    lastUpdated = (Get-Date).ToUniversalTime().ToString("o")
    status = "in_progress"
    entries = [System.Collections.Generic.List[object]]::new()
}
$entry = Add-McpSessionEntry -Session $s -RequestId "req-$ts-003-broken-state" -QueryTitle "Found 22 instances of broken remediated placeholders" -QueryText "Build fails with 146 CS1002 errors due to leftover mangled lines containing 'remediated to CQRS' junk comments inside method calls in 7 ViewModels. This is the core of 'ViewModel logic code not in CQRS issues' to remediate. Must replace with correct dispatch or service calls, add proper tests, verify grep zero, full tests green." -Status "in_progress" -Tags @("broken","remediation","compile-fix")
Add-McpAction -Entry $entry -Description "Grep found 22 'remediated to CQRS' in Chat,Connection,Log,Main, TodoListHost,Voice,Workspace VMs" -Type "design_decision" -Status "completed" -FilePath "src/McpServerManager.UI.Core/ViewModels/*.cs"
Add-McpAction -Entry $entry -Description "dotnet build shows 146 compile errors, mostly ; expected from mangled await lines. Prior partial remediation left code unbuildable." -Type "design_decision" -Status "completed"
Update-McpSessionLog -Session $s
Write-Host "Bad pattern discovery logged, req $($entry.requestId)"
