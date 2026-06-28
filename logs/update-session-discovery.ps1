Import-Module .\McpSession.psm1
Initialize-McpSession
$sessionId = "GrokCode-20260627T021310Z-remediate-viewmodel-logic-cqrs"
# We need to fetch current to get the entry. For simplicity, create update via POST or use Get + set.
# Since module supports Get, but to append action we can load or re-create entry add.
# Simpler: call Get to see, then use Add-McpAction on a new entry? But to append to existing, use direct or Add new entry for progress.
$s = [PSCustomObject]@{
    sourceType = "GrokCode"
    sessionId = $sessionId
    title = "Remediate all ViewModel logic code not in CQRS issues"
    model = "grok-4.3-2026"
}
$entry = Add-McpSessionEntry -Session $s -QueryTitle "Discovery phase" -QueryText "Inspected matrix, identified VMs with logic offenses from ViewModel Usage sheet. Top: MainWindow(134), TodoListHost(42), Workspace(32), VoiceConv(23), etc. Read AGENTS, health ok, session started, todos fetched. Plan to follow Byrd: tests-first per VM starting from listed." -Status in_progress -NoPush
Add-McpAction -Entry $entry -Description "Read AGENTS-README-FIRST.yaml, verified health, initialized session modules, created initial session turn" -Type "observation" -Status "completed" -FilePath "AGENTS-README-FIRST.yaml"
Add-McpAction -Entry $entry -Description "Queried todos via Get-McpTodo, inspected ViewModel Usage sheet via python/openpyxl for offenses>0 list" -Type "observation" -Status "completed" -FilePath "docs/McpServerManager_Management_Interfaces_Matrix_fixed.xlsx"
Add-McpAction -Entry $entry -Description "Explored UI.Core ViewModels, Handlers, Messages, DI registration, existing Workspace dispatch pattern" -Type "observation" -Status "completed"
Update-McpSessionLog -Session $s
Write-Host "Updated session with discovery actions for requestId $($entry.requestId)"
$entry | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 "logs/session-remediate-entry-discovery.json"
