cd tests/McpServerManager.UI.Core.Tests
dotnet build -c Debug --nologo -v q 2>&1 | Out-Null
$testOutput = dotnet test -c Debug --no-build --logger "console;verbosity=minimal" 2>&1
$testOutput | Select-String -Pattern "Test run|Passed|Failed|Total tests" | Select-Object -Last 10
$passed = ($testOutput | Select-String -Pattern "Passed:\s*(\d+)").Matches.Groups[1].Value
Write-Host "LAST_LINE: $passed tests or see output"
$testOutput | Out-File -Encoding utf8 ../../logs/ui-core-tests-baseline.txt
