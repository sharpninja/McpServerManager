cd tests/McpServerManager.UI.Core.Tests
dotnet build -c Debug --no-restore -v q 2>&1 | Out-Null
$testLog = dotnet test -c Debug --no-build --logger "console;verbosity=minimal" 2>&1
$testLog | Select-String -Pattern "Test run|Passed:|Failed:|Total tests" | Select -Last 5
$passedLine = $testLog | Select-String -Pattern "Passed!.*Passed:\s*(\d+)"
Write-Host "TESTS: $($passedLine.Line)"
$testLog | Out-File -Encoding utf8 ../../logs/ui-core-tests-post-remediation.txt
