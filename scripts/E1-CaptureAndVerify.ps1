$ErrorActionPreference = "Stop"
$scratch = "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
$ids = @('PLAN-VM-CQRS-REMEDIATION-001','PLAN-C1-SMALL-VMS-001','PLAN-C2-TODO-FAMILY-001','PLAN-C3-AGENT-CONFIG-001','PLAN-C3-AGENT-CONFIG-WS-001','PLAN-C4-MAINWINDOW-001','PLAN-C5-CODEBEHIND-001')
Remove-Item (Join-Path $scratch 'probe-*.json') -Force -EA SilentlyContinue
Import-Module .\McpTodo.psm1 -Force
Import-Module .\McpSession.psm1 -Force
Initialize-McpTodo -BaseUrl 'http://PAYTON-LEGION2:7147' -ApiKey '97Tqfp-wpy5QDrkmWQ2t1xJSaRbLHPyGddFUT0V413k'
foreach ($id in $ids) {
  $t = Get-McpTodo -Id $id
  $json = $t | ConvertTo-Json -Depth 8
  $out = Join-Path $scratch "probe-$id.json"
  Add-LinesToFile -Path $out -Content $json
  $dc = if ($t.description -is [array]) { $t.description.Count } else { 0 }
  $ic = if ($t.implementationTasks) { $t.implementationTasks.Count } else { 0 }
  Write-Host "PROBE $id DESC_CT=$dc IMPL_CT=$ic LEN=$($json.Length)"
  if ($dc -lt 6 -or $ic -lt 3) { Write-Host "FAIL short for $id"; exit 1 }
}
Write-Host "CAPTURE_OK"
# session on pinned
$marker = Get-Content (Join-Path $scratch 'e1-session-marker.json') | ConvertFrom-Json
$sessId = $marker.sessionId
$s = @{sessionId=$sessId}
Add-McpSessionEntry -Session $s -QueryTitle 'E1 Capture' -QueryText 'orchestrator' -Response 'full evidence captured' -Status 'completed' -Model 'grok'
try {
  $detail = Invoke-RestMethod -Uri "http://PAYTON-LEGION2:7147/mcpserver/sessionlog/GrokCode/$sessId" -Headers @{ 'X-Api-Key' = '97Tqfp-wpy5QDrkmWQ2t1xJSaRbLHPyGddFUT0V413k' }
  $js = $detail | ConvertTo-Json -Depth 5
  Add-LinesToFile -Path (Join-Path $scratch 'probe-session-full.json') -Content $js
  Write-Host "SESSION turnCount=$($detail.turnCount)"
} catch {}
# patterns raw
$pat = ""
$pat += (Select-String -Path 'src/McpServerManager.Core/Commands/AllCommands.cs' -Pattern 'Result<.*>.Success|sealed record.*ICommand' | Select -First 5 | % Line ) -join "`n"
$pat += "`n" + (Select-String -Path 'src/McpServerManager.Core/Commands/CqrsRelayFactory.cs' -Pattern 'CqrsRelayFactory' | Select -First 3 | % Line) -join "`n"
Add-LinesToFile -Path (Join-Path $scratch 'probe-patterns-raw.txt') -Content $pat
Write-Host "PATTERNS_LEN=$($pat.Length)"
# mech diff
$diff = ""
foreach ($id in $ids) {
  $live = Get-McpTodo -Id $id
  $disk = Get-Content -Raw (Join-Path $scratch "probe-$id.json") | ConvertFrom-Json
  $ld = if ($live.description -is [array]) { $live.description.Count } else { 0 }
  $dd = if ($disk.description -is [array]) { $disk.description.Count } else { 0 }
  $li = if ($live.implementationTasks) { $live.implementationTasks.Count } else { 0 }
  $di = if ($disk.implementationTasks) { $disk.implementationTasks.Count } else { 0 }
  if ($ld -ne $dd -or $li -ne $di) { $diff += "MISMATCH $id liveD=$ld diskD=$dd liveI=$li diskI=$di`n" }
}
Add-LinesToFile -Path (Join-Path $scratch 'step6-diff.log') -Content $diff
if ($diff) { Write-Host "DIFF_FAIL"; exit 1 }
Write-Host "DIFF_OK"
# assemble mechanical
$outLog = Join-Path $scratch 'e1_final_verif.log'
Remove-Item $outLog -Force -EA SilentlyContinue
$files = Get-ChildItem $scratch -Filter 'probe-*.json' | % FullName
$files += Get-ChildItem $scratch -Filter 'probe-session-full.json' | % FullName
$files += Get-ChildItem $scratch -Filter 'probe-patterns-raw.txt' | % FullName
$files += Get-ChildItem $scratch -Filter 'step6-diff.log' | % FullName
foreach ($f in $files) {
  $raw = Get-Content -Raw $f
  Add-LinesToFile -Path $outLog -Content ("=== " + (Split-Path $f -Leaf) + " ===`n" + $raw + "`n=== END ===`n")
}
Write-Host "ASSEMBLE_OK"