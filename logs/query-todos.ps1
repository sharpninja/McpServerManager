Import-Module .\McpTodo.psm1
Initialize-McpTodo
$todos = Get-McpTodo
$todos | Where-Object { $_.title -match 'ViewModel|CQRS|logic|remediat' -or $_.id -match 'VM|CQRS|UI|ARCH' } | Select-Object -Property id, title, section, done, priority | Format-Table -AutoSize
Write-Host '--- All open (done=false) ---'
$todos | Where-Object { -not $_.done } | Select-Object id, title, section | Format-Table -AutoSize
