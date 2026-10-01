# Hostile validator receipt: director stable System.CommandLine 2.0.12

TimestampUtc: 2026-10-01T16:04:42Z
ValidatorIdentity: GrokSubagentHostile
add-profile: executed yes, file count 21. Excluded add-profile.grok.md.
Work class: mixed. Class 1 is the director source migration from System.CommandLine 2.0.0-beta4 to package 2.0.12 and CommandLinePublishGuard requiring assembly >= 2.0.3.0 plus SetAction. Class 2 is the installed SharpNinja.McpServer.Director 0.6.17 DLL repair. The mixed classification is accepted. No Byrd phase, FR, TR, plan step, or TODO was claimed done.
SessionId: GrokCode-20261001T155322Z-hv-dir-stable
RequestId: req-20261001T155322Z-001-hv-director-stable-commandline
Marker: HMAC-SHA256 recomputed without printing the API key. SignatureValid=True. ComputedPrefix=E538162A29B6CBAF. Health GET returned 200, status Healthy, nonce echoed, version 1.4.41+fc75387043f8337855e34d92eb3a0e03aa82ce48.
Counts: PASS=23 FAIL=0 UNKNOWN=2 N/A=2
OverallVerdict: DISAGREE
AccuracyPercent: 98
CompletenessPercent: 98

## Why DISAGREE

AGREE requires every applicable claim to PASS and both scores to be at least 98. A3 is UNKNOWN. A11 is UNKNOWN. Either one blocks AGREE. The scores are both 98 and do not override those statuses. There is no FAIL.

## Explicit FAIL list

- none

## Explicit UNKNOWN list

- A3: The earlier wider-filter counts Failed 1, Passed 25, Skipped 0, and the connection-refused message, were not found in an artifact. A fresh run of Health_ReturnsServerStatus passed.
- A11: The three grok plugin command exit codes were not found in shell history or in a completed session turn. This review did not disable or enable the live plugin.

## Claims

### A1 A PASS

dotnet build of McpServerManager.Director Release with NuGetAudit=false exited 0 with 0 warnings and 0 errors.

Evidence: Re-ran from F:\GitHub\McpServerManager. BuildExit=0. Tail: Build succeeded. 0 Warning(s). 0 Error(s). Time Elapsed 00:00:03.24.

### A2 A PASS

The named filter exited 0 with Passed 40, Failed 0, Skipped 0.

Evidence: Same command, including --no-restore and the FullyQualifiedName filter in the brief. TestExit=0. Tail: Passed! Failed: 0, Passed: 40, Skipped: 0, Total: 40, Duration: 3 s.

### A3 A UNKNOWN

An earlier wider filter failed only Health_ReturnsServerStatus because a live health call could not connect, with counts Failed 1, Passed 25, Skipped 0. That test is not claimed green.

Evidence: No saved log of that run was found. The test uses McpServerFixture, which starts a localhost HttpListener, not PAYTON-LEGION2. Re-ran FullyQualifiedName~Health_ReturnsServerStatus. HealthExit=0. Passed: 1, Failed: 0, Skipped: 0. Output did not contain MissingMethodException and did not contain the connection-refused text. Current green does not prove or disprove the earlier counts. Not treated as a migration defect.

### A4 A PASS

Publish System.CommandLine.dll is assembly 2.0.12.0, length 151352, SHA256 2B5EE7C20FDBD08AF6C5DD7271C4E6B53FEDAB72864EE85ABEE94A647ABA704E, and is not the beta hash.

Evidence: Get-FileHash SHA256 and AssemblyName.GetAssemblyName on F:\GitHub\McpServerManager\src\McpServerManager.Director\bin\Release\net10.0\publish\System.CommandLine.dll. Length=151352. AssemblyVersion=2.0.12.0. Sha256 matched the claimed value and did not match 2158CFC2C484EBDEC95FD9959C7EECAEBE44657E1E1DEE5ABB78FC971276829F. LastWriteTimeUtc=2026-08-22T13:55:52Z, which is consistent with a preserved package timestamp, not a different file.

### A5 A PASS

Publish director.deps.json contains System.CommandLine/2.0.12 and does not contain 2.0.0-beta4.

Evidence: Read F:\GitHub\McpServerManager\src\McpServerManager.Director\bin\Release\net10.0\publish\director.deps.json. Has2012=True. HasBeta=False. Length=159691.

### A6 A PASS

The 0.6.17 nupkg entry tools/net10.0/any/System.CommandLine.dll matches A4.

Evidence: Opened F:\GitHub\McpServerManager\artifacts\director-tool\SharpNinja.McpServer.Director.0.6.17.nupkg. Entry found. Extracted length=151352. Sha256 matched A4. AssemblyVersion=2.0.12.0. Nuspec version=0.6.17. Nupkg deps.json Has2012=True and HasBeta=False. Nupkg LastWriteTimeUtc=2026-10-01T15:49:18.5898556Z.

### A7 A PASS

The installed store DLL matches A4, and the global tool list shows sharpninja.mcpserver.director 0.6.17.

Evidence: Store path in the brief. Length=151352. AssemblyVersion=2.0.12.0. Sha256 matched A4. Store director.deps.json Has2012=True and HasBeta=False. dotnet tool list --global exit 0 includes sharpninja.mcpserver.director 0.6.17 command director. Store director.dll SHA256 F962B7619F4F7A968016AB32642E796FA2F007C5D38625F2C2672DDCDE81775C matches publish director.dll, bin Release director.dll, and the nupkg director.dll. Length=966656.

### A8 A PASS

From F:\GitHub\LyftDashboard, director --help and director add-workspace --help exit 0, stderr empty, and do not contain MissingMethodException. --help stdout contains add-workspace.

Evidence: Process start of C:\Users\kingd\.dotnet\tools\director.exe with WorkingDirectory F:\GitHub\LyftDashboard. --help ExitCode=0, StdErrLength=0, StdOutLength=1903, HasAddWorkspace=True, HasMissingMethod=False. add-workspace --help ExitCode=0, StdErrLength=0, StdOutLength=457, HasMissingMethod=False. add-workspace was not run without --help. No workspace was registered.

### A9 A PASS

Beta call sites are gone from src *.cs, and the director package reference is System.CommandLine 2.0.12.

Evidence: Grep of src *.cs for SetHandler returned no matches. Grep for .SetHandler(, .AddCommand(, .AddOption(, .AddArgument(, and .AddAlias( returned no matches. An unanchored AddCommand search hits only BuildAddCommand at src\McpServerManager.Director\Commands\DirectorCommands.cs lines 31 and 257, which is a local method name, not Command.AddCommand. PackageReference is at src\McpServerManager.Director\McpServerManager.Director.csproj line 23, Version 2.0.12. Program.cs line 49 calls rootCommand.Parse(args).InvokeAsync().

### A10 A PASS

MinimumStableVersion is 2.0.3.0, EnsureCompatible requires SetAction, and those tests are inside the passing A2 filter. The full Director suite is not claimed green.

Evidence: build\CommandLinePublishGuard.cs line 15 sets MinimumStableVersion to new Version(2, 0, 3, 0). EnsureCompatible returns false unless IsSupportedVersion and DeclaresSetAction. CommandLinePublishGuardTests rejects 2.0.0.0, accepts >= 2.0.12 for the referenced assembly, and accepts the 2.0.9 fixture. Those tests match FullyQualifiedName~CommandLinePublishGuard and ran inside the 40 passing tests. This review did not run the unfiltered Director suite and does not call it green.

### A11 A UNKNOWN

grok plugin update, disable, and enable mcpserver each exited 0, and hooks.json was not edited. The code-verify.ps1 empty-expansion bug is not claimed fixed.

Evidence: ConsoleHost PSReadLine history has 0 lines containing the text grok plugin. Session turns req-20261001T151342Z-003-reload-grok-plugin and req-20261001T152123Z-004-reload-grok-plugin were still in_progress with a null response when queried. This review did not re-run disable or enable. F:\GitHub\mcpserver-grok-plugin\hooks\hooks.json LastWriteTimeUtc=2026-10-01T11:13:34Z, before the 15:04 migration turn. Its uncommitted diff changes timeout 20 to 10 and a status message. It does not change the code-verify.ps1 path. That supports "not edited during this reload" but does not prove the three exit codes. The empty-expansion bug is not treated as fixed.

### A12 A PASS

PLAN-WEBCHAT-001 was not marked done. build.ps1 NU1903 and Nuke CS0649/CS0169 are not claimed fixed. The A6 nupkg was produced by dotnet pack at PackageVersion 0.6.17, not by build.ps1.

Evidence: mcpserver todo_list done=false returned only PLAN-WEBCHAT-001 with Done=false and totalCount=1. This review did not update it. The brief does not claim those build failures fixed, and this review did not re-run build.ps1. obj\Release\SharpNinja.McpServer.Director.0.6.17.nuspec LastWriteTimeUtc=2026-10-01T15:49:16.2184539Z, two seconds before the nupkg. Nuspec version=0.6.17. Nuke ArtifactsDirectoryPath is artifacts, and PackDirectorTool writes there. The string director-tool is not in the nuke build sources searched. The nupkg path artifacts\director-tool matches an explicit dotnet pack output directory, not the nuke default. GitVersion.yml next-version is 0.6.17.

### A13 A PASS

The desired state is stable 2.0.12, not the earlier beta pin.

Evidence: Director csproj PackageReference is 2.0.12. The guard rejects assembly 2.0.0.0. Publish, nupkg, and installed deps.json contain 2.0.12 and do not contain 2.0.0-beta4. The prior session decision that said keep beta4 is superseded by the bytes on disk.

### B1 B N/A

Byrd v4 phase-order is not scored by post-hoc timestamps, and no phase was claimed complete.

Evidence: hostile-phase-gates says a late review must not FAIL phase-order solely by file times, and may FAIL only a claimed phase that lacks an inter-phase AGREE. The implementer claimed neither a phase nor a TODO done. No FR-createdAt comparison was used as a FAIL.

### B2 B PASS

Re-verifiable claims were re-run or re-read. Missing historical logs are A3 and A11, not a second FAIL.

Evidence: Build, filtered tests, health retest, hashes, deps.json text search, tool list, and both help processes were executed in this review. Git status did not show docs/todo.yaml.

### B3 B PASS

MCP-only storage was respected for TODO and session log. PLAN-WEBCHAT-001 was not edited.

Evidence: todo_list done=false still returns only PLAN-WEBCHAT-001, Done=false. This review opened session GrokCode-20261001T155322Z-hv-dir-stable through sessionlog_open and sessionlog_begin_turn. No TODO or session-log storage file was edited.

### B4 B PASS

This review used pwsh. Python was not used.

Evidence: HMAC, hashes, build, test, help, and receipt serialization ran in pwsh. No python, python3, or py invocation was used.

### B5 B PASS

No unrelated delete was observed. Temp extracts created by this review were removed.

Evidence: Nupkg entry extracts under the temp directory were deleted after hashing. add-workspace was not executed. The publish ClearDirectory call remains in the nuke pipeline and was not invoked by this review.

### B6 B PASS

Measured claims that were re-run matched. No contradicted hash, count, or exit code was found.

Evidence: A1, A2, A4, A5, A6, A7, and A8 matched the stated numbers. A3 and A11 are unverified, not disproven.

### B7 B PASS

New guard source does not add an em dash or en dash.

Evidence: build\CommandLinePublishGuard.cs and tests\McpServerManager.Director.Tests\CommandLinePublishGuardTests.cs do not contain U+2014 or U+2013. Pre-existing em dashes remain in older director screens and were not treated as this migration.

### B8 B PASS

This review's request jsonl stores the full brief in prompt.

Evidence: The request jsonl prompt length is recorded by the writer script and includes the opening and closing sentences of the brief. It is not a promptSummary-only file.

### C1 C N/A

No FR or TR was claimed complete. The class 2 install is not failed for a missing requirement. The class 1 migration was not claimed as a completed requirement.

Evidence: requirements_list type=fr has no FR-MCP-030. The type=tr payload has no TR-MCP-DIR-001 and no System.CommandLine hit. docs\Requirements-Director.md still describes both IDs as complete System.CommandLine coverage. That drift was not claimed fixed. This review created no requirements. Suite green was not treated as AC coverage.

### D1 D PASS

No plan step for this bug was claimed done. PLAN-WEBCHAT-001 stays not done. Its definition of done was not applied.

Evidence: Active plan for this bug: none. todo_list done=false on this run returned only PLAN-WEBCHAT-001, Done=false, totalCount=1. This review did not mark it done.

## Scores

AccuracyPercent 98: the re-measured build, test counts, hashes, assembly versions, package reference, guard contract, and help exits matched. Two points are withheld because the earlier health-filter story and the plugin exit codes have no receipt.

CompletenessPercent 98: source, tests, nupkg, and the installed 0.6.17 tool are on stable 2.0.12, and --help works from LyftDashboard. Two points are withheld because the plugin reload and the earlier wider-filter log were not in the delivered evidence. The full Director suite was correctly not claimed green.