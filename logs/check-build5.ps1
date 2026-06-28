$buildLog = dotnet build src/McpServerManager.UI.Core/McpServerManager.UI.Core.csproj -c Debug --no-restore -v minimal 2>&1
$errs = $buildLog | Select-String -Pattern "error CS" 
Write-Host "CS errors: $($errs.Count)"
$errs | Out-File -Encoding utf8 logs/current-build-errs.txt
Get-Content logs/current-build-errs.txt 
