$buildLog = dotnet build src/McpServerManager.UI.Core/McpServerManager.UI.Core.csproj -c Debug --no-restore -v minimal 2>&1
$buildLog | Select-String -Pattern "(error CS|Build FAILED|ChatWindow|ConnectionViewModel.cs)" | Select -First 20
$errCount = ($buildLog | Select-String -Pattern "error CS").Count
Write-Host "CS error count: $errCount"
if ($errCount -gt 0) { $buildLog | Out-File -Encoding utf8 logs/build-ui-core-errors.txt }
