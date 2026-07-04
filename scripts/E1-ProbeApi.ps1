$ErrorActionPreference = "Stop"
$scratch = "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
Import-Module .\McpSession.psm1 -Force
Import-Module .\McpTodo.psm1 -Force
Initialize-McpTodo -BaseUrl "http://PAYTON-LEGION2:7147" -ApiKey "97Tqfp-wpy5QDrkmWQ2t1xJSaRbLHPyGddFUT0V413k"
$todo = Get-McpTodo -Id "PLAN-VM-CQRS-REMEDIATION-001"
$json = $todo | ConvertTo-Json -Depth 8
$out = Join-Path $scratch "probe-PLAN-VM-CQRS-REMEDIATION-001.json"
Remove-Item $out -Force -ErrorAction SilentlyContinue
Add-LinesToFile -Path $out -Content $json
Write-Host "PROBE_MAIN_OK descLines=$(if($todo.description -is [array]){$todo.description.Count}else{0})"