$ErrorActionPreference = "Stop"
$scratch = "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
Import-Module .\McpSession.psm1 -Force
Import-Module .\McpTodo.psm1 -Force
Initialize-McpSession -BaseUrl "http://PAYTON-LEGION2:7147" -ApiKey "97Tqfp-wpy5QDrkmWQ2t1xJSaRbLHPyGddFUT0V413k"
$s = New-McpSessionLog -SourceType "GrokCode" -Title "E1 SessionProof" -Model "grok"
$longResp = "E1 patterns: Result<T>.Success/Failure in AllCommands.cs TodoCommands.cs. sealed record Cmd : ICommand<ResultDTO>; sealed class Handler : ICommandHandler<Cmd,ResultDTO>. No VM refs in commands. CqrsRelayFactory dispatch. VMs pure state + dispatch only. Matrix: PS obj Convert $var1. C* TODOs full detailed. All root Mcp + pwsh MCP."
$entry = Add-McpSessionEntry -Session $s -QueryTitle "E1 SessionProof" -QueryText "rich turn for verif" -Response $longResp -Status "completed" -Model "grok"
$js = $s | ConvertTo-Json -Depth 4
Add-LinesToFile -Path (Join-Path $scratch "probe-session.json") -Content $js
Write-Host "SESSION_PROOF_OK"