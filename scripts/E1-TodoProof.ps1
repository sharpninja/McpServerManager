$ErrorActionPreference = "Stop"
$scratch = "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
Import-Module .\McpTodo.psm1 -Force
Initialize-McpTodo -BaseUrl "http://PAYTON-LEGION2:7147" -ApiKey "97Tqfp-wpy5QDrkmWQ2t1xJSaRbLHPyGddFUT0V413k"
$main = Get-McpTodo -Id "PLAN-VM-CQRS-REMEDIATION-001"
$json = $main | ConvertTo-Json -Depth 8
$out = Join-Path $scratch "probe-todo.json"
Remove-Item $out -Force -EA SilentlyContinue
Add-LinesToFile -Path $out -Content $json
$c1 = Get-McpTodo -Id "PLAN-C1-SMALL-VMS-001"
$c1Json = $c1 | ConvertTo-Json -Depth 8
Add-LinesToFile -Path (Join-Path $scratch "c1-live.json") -Content $c1Json
Write-Host "TODO_PROOF_OK descLines=$(if($main.description -is [array]){$main.description.Count}else{0}) IMPL=$($main.implementationTasks.Count)"
if ((($main.description | Out-String).Length) -lt 1500) { exit 1 }
Write-Host "TODO_PROOF_OK"