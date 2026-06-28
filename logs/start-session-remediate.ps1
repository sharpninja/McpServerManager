Import-Module .\McpSession.psm1
Initialize-McpSession
$ts = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$slug = 'remediate-viewmodel-logic-cqrs'
$sessionId = "GrokCode-$ts-$slug"
$s = New-McpSessionLog -SourceType 'GrokCode' -SessionId $sessionId -Title 'Remediate all ViewModel logic code not in CQRS issues' -Model 'grok-4.3-2026'
$reqId = "req-$ts-001-remediate-vm-cqrs"
$entry = Add-McpSessionEntry -Session $s -RequestId $reqId -QueryTitle 'remediate all ViewModel logic code not in CQRS issues' -QueryText 'remediate all ViewModel logic code not in CQRS issues per plan using Byrd TDD process' -Status 'in_progress' -Tags @('cqrs','viewmodel','remediation','byrd-tdd') -Interpretation 'Follow Agents.md strictly: use session logs/todos via modules, Byrd process (tests first, validate mocks, implement, all green), read plan.md + matrix, identify remaining ViewModels with logic outside CQRS, write tests first for dispatch extraction to CQRS handlers.'
Write-Host "Session created: $($s.sessionId)"
Write-Host "Entry requestId: $($entry.requestId)"
$s | ConvertTo-Json -Depth 3 | Out-File -Encoding utf8 "logs/session-remediate-vm-cqrs-start.json"
$entry | ConvertTo-Json -Depth 3 | Out-File -Encoding utf8 "logs/session-remediate-vm-cqrs-entry-start.json"
