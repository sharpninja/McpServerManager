# Android Crash Diagnostics Workflow

This workflow is for reproducing and collecting phone-app crashes on the attached Motorola Edge (`ZD222QH58Q` by default).

## In-App Diagnostics

The Android app now writes crash diagnostics under:

`/data/data/ninja.thesharp.mcpservermanager/files/diagnostics/crash`

Artifacts include:

- `pending-fatal-report.json`: last fatal managed or Java uncaught exception captured by the shared crash handler.
- `pending-exit-info.json`: last interesting `ApplicationExitInfo` recovered on the next launch.
- `pending-boundary.json`: last unfinished native-sensitive boundary carried across launches.
- `fatal-*.json`: timestamped fatal crash snapshots.
- `diagnostic-*.json`: timestamped non-fatal diagnostic events.

Native-sensitive operations keep an active boundary while they run so a later native crash can be correlated with the last active Porcupine or Vosk workload.

## Reproduction Loop

1. Prepare a clean capture directory and clear existing logcat noise:

```powershell
.\scripts\collect-android-crash-artifacts.ps1 -Phase Prepare
```

2. Reproduce the crash on the Motorola Edge.

3. Launch the app once after the crash.
The app will replay any persisted fatal report and process-exit info into the in-app log/status surfaces.

4. Collect the artifacts:

```powershell
.\scripts\collect-android-crash-artifacts.ps1 -Phase Collect -OutputRoot .\artifacts\android-crash\<timestamp>
```

5. If the crash looks native or ANR-related, rerun collection with a bugreport:

```powershell
.\scripts\collect-android-crash-artifacts.ps1 -Phase Collect -OutputRoot .\artifacts\android-crash\<timestamp> -IncludeBugreport
```

## What The Collector Captures

- `adb devices -l`
- full `getprop`
- `dumpsys package <package>`
- full `logcat -d -b all -v threadtime`
- `dumpsys meminfo <package>`
- `dumpsys activity exit-info <package>`
- `/data/tombstones` directory listing attempt
- app-sandbox crash diagnostics via `run-as` for text artifacts
- optional `adb bugreport`

## Notes

- `run-as ninja.thesharp.mcpservermanager` is expected to work best with Debug installs.
- Retail devices may block direct tombstone reads; in that case the bugreport is the fallback artifact.
- The shared crash handler intentionally rethrows fatal callback exceptions after persisting diagnostics so Android runtime behavior remains unchanged.
