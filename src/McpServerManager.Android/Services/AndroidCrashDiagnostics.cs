using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using JThread = Java.Lang.Thread;
using StatusViewModel = McpServer.UI.Core.ViewModels.StatusViewModel;
using SysEnvironment = System.Environment;
using Throwable = Java.Lang.Throwable;

namespace McpServerManager.Android.Services;

/// <summary>
/// Captures Android crash diagnostics early in process startup and replays persisted
/// fatal reports after the next successful launch.
/// </summary>
public static class AndroidCrashDiagnostics
{
    private const string LogTag = "McpSM";
    private const string DiagnosticsDirectoryName = "diagnostics";
    private const string CrashDirectoryName = "crash";
    private const string PendingFatalFileName = "pending-fatal-report.json";
    private const string PendingExitInfoFileName = "pending-exit-info.json";
    private const string PendingBoundaryFileName = "pending-boundary.json";
    private const string ActiveBoundaryFileName = "active-boundary.json";
    private const string ExitInfoCursorFileName = "last-exit-timestamp.txt";
    private const int MaxProcessStateSummaryBytes = 128;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("AndroidCrashDiagnostics");
    private static readonly object SyncRoot = new();

    private static Context? _context;
    private static CrashUncaughtExceptionHandler? _uncaughtExceptionHandler;
    private static int _initialized;
    private static int _managedHandlersInstalled;
    private static string? _lastFatalFingerprint;
    private static DateTimeOffset _lastFatalRecordedAtUtc;

    /// <summary>Relative path inside the app sandbox used by the adb collection workflow.</summary>
    public static string DiagnosticsRelativePath => $"files/{DiagnosticsDirectoryName}/{CrashDirectoryName}";

    /// <summary>Initializes crash diagnostics during Android application startup.</summary>
    public static void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        _context = application.ApplicationContext ?? application;
        EnsureDiagnosticsDirectory();
        RotatePendingBoundaryFromPreviousProcess();
        InstallManagedHandlers();
        InstallJavaUncaughtExceptionHandler();
        CaptureHistoricalExitInfo();
        LogEarly("Android crash diagnostics initialized.");
    }

    /// <summary>
    /// Replays persisted crash diagnostics into the in-app log/status surfaces after
    /// the UI logging bridge is active.
    /// </summary>
    public static void ReplayPendingDiagnostics()
    {
        ReplayPendingFatalReport();
        ReplayPendingExitInfo();
        ReplayPendingBoundary();
    }

    /// <summary>
    /// Executes a fatal callback path through the shared crash processor and rethrows the
    /// original exception so Android runtime behavior remains unchanged.
    /// </summary>
    public static void ExecuteFatal(string source, Action action, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (Exception ex)
        {
            RecordManagedFatal(source, ex, detail);
            throw;
        }
    }

    /// <summary>
    /// Executes a fatal callback path through the shared crash processor and rethrows the
    /// original exception so Android runtime behavior remains unchanged.
    /// </summary>
    public static T ExecuteFatal<T>(string source, Func<T> action, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            RecordManagedFatal(source, ex, detail);
            throw;
        }
    }

    /// <summary>
    /// Executes an async fatal callback path through the shared crash processor and rethrows the
    /// original exception so Android runtime behavior remains unchanged.
    /// </summary>
    public static async Task ExecuteFatalAsync(string source, Func<Task> action, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            RecordManagedFatal(source, ex, detail);
            throw;
        }
    }

    /// <summary>
    /// Marks a crash-sensitive operation so that a later native or abrupt process death
    /// can be correlated with the last active operation.
    /// </summary>
    public static IDisposable BeginBoundary(string name, string? detail = null)
    {
        var lease = new BoundaryLease(Guid.NewGuid().ToString("N"), name, detail);
        TrySetActiveBoundary(lease.State);
        return lease;
    }

    /// <summary>Persists a fatal managed exception for recovery on the next launch.</summary>
    public static void RecordManagedFatal(string source, Exception? exception, string? detail = null)
    {
        ProcessCrashEvent(new CrashEvent
        {
            Source = source,
            Severity = LogLevel.Critical,
            Kind = CrashEventKind.Fatal,
            Message = exception?.Message,
            Detail = detail,
            ExceptionType = exception?.GetType().FullName,
            StackTrace = exception?.ToString(),
            ThreadName = System.Threading.Thread.CurrentThread.Name ?? $"managed-{SysEnvironment.CurrentManagedThreadId}",
            ThreadId = SysEnvironment.CurrentManagedThreadId,
            StatusText = exception?.ToString() ?? detail ?? "Unknown unhandled exception"
        });
    }

    /// <summary>Persists a fatal Java-side uncaught exception for recovery on the next launch.</summary>
    public static void RecordJavaFatal(string source, JThread? thread, Throwable? throwable, string? detail = null)
    {
        ProcessCrashEvent(new CrashEvent
        {
            Source = source,
            Severity = LogLevel.Critical,
            Kind = CrashEventKind.Fatal,
            Message = throwable?.Message,
            Detail = detail,
            ExceptionType = throwable?.Class?.CanonicalName ?? throwable?.Class?.Name,
            StackTrace = throwable?.ToString(),
            JavaStackTrace = RenderThrowable(throwable),
            ThreadName = thread?.Name,
            ThreadId = SafeThreadId(thread),
            StatusText = throwable?.ToString() ?? detail ?? "Unknown Android Java unhandled exception"
        });
    }

    /// <summary>Persists a non-fatal background diagnostic event for later inspection.</summary>
    public static void RecordDiagnosticEvent(string source, Exception? exception, string? detail = null)
    {
        ProcessCrashEvent(new CrashEvent
        {
            Source = source,
            Severity = LogLevel.Error,
            Kind = CrashEventKind.Diagnostic,
            Message = exception?.Message,
            Detail = detail,
            ExceptionType = exception?.GetType().FullName,
            StackTrace = exception?.ToString(),
            ThreadName = System.Threading.Thread.CurrentThread.Name ?? $"managed-{SysEnvironment.CurrentManagedThreadId}",
            ThreadId = SysEnvironment.CurrentManagedThreadId,
            StatusText = exception?.ToString() ?? detail ?? "Background exception"
        });
    }

    private static void InstallManagedHandlers()
    {
        if (Interlocked.Exchange(ref _managedHandlersInstalled, 1) == 1)
            return;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            RecordManagedFatal(
                "AppDomain.CurrentDomain.UnhandledException",
                exception,
                args.ExceptionObject?.ToString());
        };

        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            RecordManagedFatal(
                "Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser",
                args.Exception,
                "Managed exception raised through Android runtime bridge.");
            args.Handled = false;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            RecordDiagnosticEvent(
                "TaskScheduler.UnobservedTaskException",
                args.Exception,
                "Faulted background task was finalized without observation.");
            args.SetObserved();
        };
    }

    private static void InstallJavaUncaughtExceptionHandler()
    {
        try
        {
            var previous = JThread.DefaultUncaughtExceptionHandler;
            if (previous is CrashUncaughtExceptionHandler)
                return;

            _uncaughtExceptionHandler = new CrashUncaughtExceptionHandler(previous);
            JThread.DefaultUncaughtExceptionHandler = _uncaughtExceptionHandler;
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to install default Java uncaught exception handler: {ex}");
        }
    }

    private static FatalCrashReport CreateFatalReport(
        string source,
        string? exceptionType,
        string? message,
        string? detail,
        string? stackTrace,
        string? javaStackTrace,
        string? threadName,
        int? threadId)
    {
        var boundary = TryReadBoundaryState();

        return new FatalCrashReport
        {
            Source = source,
            TimestampUtc = DateTimeOffset.UtcNow,
            ProcessId = global::Android.OS.Process.MyPid(),
            ThreadName = threadName,
            ThreadId = threadId,
            ExceptionType = exceptionType,
            Message = message,
            Detail = detail,
            StackTrace = stackTrace,
            JavaStackTrace = javaStackTrace,
            ActiveBoundary = boundary,
            DeviceManufacturer = Build.Manufacturer,
            DeviceModel = Build.Model,
            AndroidRelease = Build.VERSION.Release,
            PackageName = _context?.PackageName
        };
    }

    private static void ProcessCrashEvent(CrashEvent crashEvent)
    {
        try
        {
            if (crashEvent.Kind == CrashEventKind.Fatal)
            {
                var report = CreateFatalReport(
                    crashEvent.Source,
                    crashEvent.ExceptionType,
                    crashEvent.Message,
                    crashEvent.Detail,
                    crashEvent.StackTrace,
                    crashEvent.JavaStackTrace,
                    crashEvent.ThreadName,
                    crashEvent.ThreadId);

                if (ShouldPersistFatal(report))
                    PersistFatalReport(report);
            }
            else
            {
                PersistDiagnosticEvent(crashEvent);
            }
        }
        catch (Exception ex)
        {
            LogEarly($"Failed while processing crash event '{crashEvent.Source}': {ex}");
        }

        PublishEvent(crashEvent);
    }

    private static void PersistDiagnosticEvent(CrashEvent crashEvent)
    {
        try
        {
            var artifact = new DiagnosticEventArtifact
            {
                Source = crashEvent.Source,
                Detail = crashEvent.Detail,
                TimestampUtc = DateTimeOffset.UtcNow,
                Message = crashEvent.Message,
                ExceptionType = crashEvent.ExceptionType,
                StackTrace = crashEvent.StackTrace,
                ActiveBoundary = TryReadBoundaryState()
            };

            var fileName = $"diagnostic-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.json";
            WriteArtifact(fileName, artifact);
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to persist diagnostic event '{crashEvent.Source}': {ex}");
        }
    }

    private static bool ShouldPersistFatal(FatalCrashReport report)
    {
        lock (SyncRoot)
        {
            var fingerprint = string.Join(
                "|",
                report.Source,
                report.ExceptionType,
                report.Message,
                report.ThreadName,
                report.ActiveBoundary?.Name,
                report.ActiveBoundary?.Detail);

            if (string.Equals(fingerprint, _lastFatalFingerprint, StringComparison.Ordinal) &&
                report.TimestampUtc - _lastFatalRecordedAtUtc <= TimeSpan.FromSeconds(5))
            {
                return false;
            }

            _lastFatalFingerprint = fingerprint;
            _lastFatalRecordedAtUtc = report.TimestampUtc;
            return true;
        }
    }

    private static void PersistFatalReport(FatalCrashReport report)
    {
        try
        {
            var timestamp = report.TimestampUtc.ToString("yyyyMMdd-HHmmss-fff");
            WriteArtifact($"fatal-{timestamp}.json", report);
            WriteArtifact(PendingFatalFileName, report);
            LogEarly($"Persisted fatal crash report from {report.Source}.");
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to persist fatal crash report: {ex}");
        }
    }

    private static void PublishEvent(CrashEvent crashEvent)
    {
        var rendered = RenderEvent(crashEvent);
        if (string.IsNullOrWhiteSpace(rendered.LogMessage))
            return;

        switch (crashEvent.Severity)
        {
            case LogLevel.Trace:
                Logger.LogTrace("{CrashEvent}", rendered.LogMessage);
                break;
            case LogLevel.Debug:
                Logger.LogDebug("{CrashEvent}", rendered.LogMessage);
                break;
            case LogLevel.Information:
                Logger.LogInformation("{CrashEvent}", rendered.LogMessage);
                break;
            case LogLevel.Warning:
                Logger.LogWarning("{CrashEvent}", rendered.LogMessage);
                break;
            case LogLevel.Error:
                Logger.LogError("{CrashEvent}", rendered.LogMessage);
                break;
            default:
                Logger.LogCritical("{CrashEvent}", rendered.LogMessage);
                break;
        }

        if (!string.IsNullOrWhiteSpace(rendered.StatusMessage))
            StatusViewModel.Instance.AddStatus(rendered.StatusMessage);
    }

    private static RenderedCrashEvent RenderEvent(CrashEvent crashEvent)
    {
        var lines = new List<string>
        {
            $"[{crashEvent.Kind}] {crashEvent.Source}"
        };

        if (!string.IsNullOrWhiteSpace(crashEvent.ExceptionType))
            lines.Add($"Exception: {crashEvent.ExceptionType}");
        if (!string.IsNullOrWhiteSpace(crashEvent.Message))
            lines.Add($"Message: {crashEvent.Message}");
        if (!string.IsNullOrWhiteSpace(crashEvent.Detail))
            lines.Add($"Detail: {crashEvent.Detail}");
        if (!string.IsNullOrWhiteSpace(crashEvent.ThreadName))
            lines.Add($"Thread: {crashEvent.ThreadName} ({crashEvent.ThreadId?.ToString() ?? "n/a"})");

        var boundary = TryReadBoundaryState();
        if (boundary != null)
        {
            lines.Add(
                $"Boundary: {boundary.Name} @ {boundary.StartedUtc:O}" +
                (string.IsNullOrWhiteSpace(boundary.Detail) ? string.Empty : $" ({boundary.Detail})"));
        }

        if (!string.IsNullOrWhiteSpace(crashEvent.StackTrace))
            lines.Add(crashEvent.StackTrace);
        if (!string.IsNullOrWhiteSpace(crashEvent.JavaStackTrace) &&
            !string.Equals(crashEvent.JavaStackTrace, crashEvent.StackTrace, StringComparison.Ordinal))
            lines.Add(crashEvent.JavaStackTrace);

        return new RenderedCrashEvent
        {
            LogMessage = string.Join(SysEnvironment.NewLine, lines),
            StatusMessage = crashEvent.StatusText
        };
    }

    private static void CaptureHistoricalExitInfo()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R)
            return;

        try
        {
            if (_context == null)
                return;

            var activityManager = _context.GetSystemService(Context.ActivityService) as ActivityManager;
            if (activityManager == null)
                return;

            var exitInfos = activityManager.GetHistoricalProcessExitReasons(_context.PackageName, 0, 5);
            if (exitInfos == null || exitInfos.Count == 0)
                return;

            var newest = exitInfos
                .OrderByDescending(info => info.Timestamp)
                .FirstOrDefault();

            if (newest == null)
                return;

            var lastSeenTimestamp = ReadLastSeenExitTimestamp();
            if (newest.Timestamp <= lastSeenTimestamp)
                return;

            WriteLastSeenExitTimestamp(newest.Timestamp);

            if (!IsInterestingExit(newest))
                return;

            string? traceArtifact = null;
            try
            {
                traceArtifact = PersistExitTrace(newest);
            }
            catch (Exception ex)
            {
                LogEarly($"Failed to persist ApplicationExitInfo trace: {ex}");
            }

            var artifact = new ProcessExitInfoArtifact
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ExitTimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(newest.Timestamp),
                ProcessName = newest.ProcessName,
                ProcessId = newest.Pid,
                Reason = newest.Reason,
                ReasonLabel = DescribeReason(GetExitReason(newest)),
                Status = newest.Status,
                Importance = newest.Importance,
                Description = newest.Description,
                PssKb = newest.Pss,
                RssKb = newest.Rss,
                TraceArtifactFile = traceArtifact,
                ProcessStateSummary = DecodeProcessStateSummary(newest)
            };

            var timestamp = artifact.CapturedAtUtc.ToString("yyyyMMdd-HHmmss-fff");
            WriteArtifact($"exit-info-{timestamp}.json", artifact);
            WriteArtifact(PendingExitInfoFileName, artifact);
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to capture historical process exit info: {ex}");
        }
    }

    private static void ReplayPendingFatalReport()
    {
        var path = GetPendingArtifactPath(PendingFatalFileName);
        var report = TryReadArtifact<FatalCrashReport>(path);
        if (report == null)
            return;

        try
        {
            var lines = new List<string>
            {
                $"Recovered fatal crash report from previous launch: {report.Source}",
                $"UTC: {report.TimestampUtc:O}",
                $"Exception: {report.ExceptionType ?? "Unknown"}",
                $"Message: {report.Message ?? "(none)"}"
            };

            if (report.ActiveBoundary != null)
            {
                lines.Add(
                    $"Active boundary at failure: {report.ActiveBoundary.Name} @ {report.ActiveBoundary.StartedUtc:O}" +
                    (string.IsNullOrWhiteSpace(report.ActiveBoundary.Detail) ? string.Empty : $" ({report.ActiveBoundary.Detail})"));
            }

            if (!string.IsNullOrWhiteSpace(report.StackTrace))
                lines.Add(report.StackTrace);

            PublishEvent(new CrashEvent
            {
                Source = $"Recovered/{report.Source}",
                Severity = LogLevel.Critical,
                Kind = CrashEventKind.RecoveredFatal,
                Message = report.Message,
                Detail = $"Recovered fatal crash from previous launch at {report.TimestampUtc:O}",
                ExceptionType = report.ExceptionType,
                StackTrace = string.Join(SysEnvironment.NewLine, lines),
                ThreadName = report.ThreadName,
                ThreadId = report.ThreadId,
                StatusText = $"Recovered fatal crash: {report.ExceptionType ?? report.Source}"
            });
        }
        finally
        {
            SafeDelete(path);
        }
    }

    private static void ReplayPendingExitInfo()
    {
        var path = GetPendingArtifactPath(PendingExitInfoFileName);
        var artifact = TryReadArtifact<ProcessExitInfoArtifact>(path);
        if (artifact == null)
            return;

        try
        {
            var summary = new StringBuilder();
            summary.Append("Recovered previous Android process exit: ");
            summary.Append(artifact.ReasonLabel);
            summary.Append(" @ ");
            summary.Append(artifact.ExitTimestampUtc.ToString("O"));
            summary.Append(" (status=");
            summary.Append(artifact.Status);
            summary.Append(", importance=");
            summary.Append(artifact.Importance);
            summary.Append(')');

            if (!string.IsNullOrWhiteSpace(artifact.Description))
            {
                summary.Append(SysEnvironment.NewLine);
                summary.Append("Description: ");
                summary.Append(artifact.Description);
            }

            if (!string.IsNullOrWhiteSpace(artifact.ProcessStateSummary))
            {
                summary.Append(SysEnvironment.NewLine);
                summary.Append("Last process-state summary: ");
                summary.Append(artifact.ProcessStateSummary);
            }

            if (!string.IsNullOrWhiteSpace(artifact.TraceArtifactFile))
            {
                summary.Append(SysEnvironment.NewLine);
                summary.Append("Trace artifact: ");
                summary.Append(artifact.TraceArtifactFile);
            }

            PublishEvent(new CrashEvent
            {
                Source = "Recovered/ApplicationExitInfo",
                Severity = LogLevel.Warning,
                Kind = CrashEventKind.RecoveredExit,
                Message = artifact.ReasonLabel,
                Detail = summary.ToString(),
                StatusText = $"Previous Android exit: {artifact.ReasonLabel}"
            });
        }
        finally
        {
            SafeDelete(path);
        }
    }

    private static void ReplayPendingBoundary()
    {
        var path = GetPendingArtifactPath(PendingBoundaryFileName);
        var artifact = TryReadArtifact<BoundaryState>(path);
        if (artifact == null)
            return;

        try
        {
            PublishEvent(new CrashEvent
            {
                Source = "Recovered/Boundary",
                Severity = LogLevel.Warning,
                Kind = CrashEventKind.RecoveredBoundary,
                Message = artifact.Name,
                Detail =
                    $"Recovered unfinished crash boundary from previous launch: {artifact.Name} @ {artifact.StartedUtc:O}" +
                    (string.IsNullOrWhiteSpace(artifact.Detail) ? string.Empty : $" ({artifact.Detail})"),
                ThreadName = artifact.ThreadName,
                StatusText = $"Recovered unfinished boundary: {artifact.Name}"
            });
        }
        finally
        {
            SafeDelete(path);
        }
    }

    private static void RotatePendingBoundaryFromPreviousProcess()
    {
        var activePath = GetArtifactPath(ActiveBoundaryFileName);
        if (!File.Exists(activePath))
            return;

        try
        {
            var pendingPath = GetPendingArtifactPath(PendingBoundaryFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.Copy(activePath, pendingPath, overwrite: true);
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to rotate previous active boundary: {ex}");
        }
        finally
        {
            SafeDelete(activePath);
        }
    }

    private static void TrySetActiveBoundary(BoundaryState state)
    {
        try
        {
            WriteArtifact(ActiveBoundaryFileName, state);
            TrySetProcessStateSummary(state);
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to set active crash boundary '{state.Name}': {ex}");
        }
    }

    private static void TryClearActiveBoundary(string leaseId)
    {
        try
        {
            var path = GetArtifactPath(ActiveBoundaryFileName);
            var active = TryReadArtifact<BoundaryState>(path);
            if (active?.LeaseId != leaseId)
                return;

            SafeDelete(path);
            TryClearProcessStateSummary();
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to clear active crash boundary '{leaseId}': {ex}");
        }
    }

    private static void TrySetProcessStateSummary(BoundaryState state)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R || _context == null)
            return;

        try
        {
            var activityManager = _context.GetSystemService(Context.ActivityService) as ActivityManager;
            if (activityManager == null)
                return;

            var summaryText = $"{state.Name}|{state.Detail}".TrimEnd('|');
            var summaryBytes = Encoding.UTF8.GetBytes(summaryText);
            if (summaryBytes.Length > MaxProcessStateSummaryBytes)
            {
                summaryText = TruncateUtf8(summaryText, MaxProcessStateSummaryBytes);
                summaryBytes = Encoding.UTF8.GetBytes(summaryText);
            }

            activityManager.SetProcessStateSummary(summaryBytes);
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to set process-state summary: {ex}");
        }
    }

    private static void TryClearProcessStateSummary()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R || _context == null)
            return;

        try
        {
            var activityManager = _context.GetSystemService(Context.ActivityService) as ActivityManager;
            activityManager?.SetProcessStateSummary(Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            LogEarly($"Failed to clear process-state summary: {ex}");
        }
    }

    private static BoundaryState? TryReadBoundaryState()
    {
        return TryReadArtifact<BoundaryState>(GetArtifactPath(ActiveBoundaryFileName));
    }

    private static string? PersistExitTrace(ApplicationExitInfo exitInfo)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R)
            return null;

        using var traceStream = exitInfo.TraceInputStream;
        if (traceStream == null)
            return null;

        var fileName = $"exit-trace-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.trace";
        var path = GetArtifactPath(fileName);

        using var output = File.Create(path);
        traceStream.CopyTo(output);
        return Path.GetFileName(path);
    }

    private static string? DecodeProcessStateSummary(ApplicationExitInfo exitInfo)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R)
            return null;

        try
        {
            var summary = exitInfo.GetProcessStateSummary();
            if (summary == null || summary.Length == 0)
                return null;

            return Encoding.UTF8.GetString(summary);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsInterestingExit(ApplicationExitInfo exitInfo)
    {
        var reason = GetExitReason(exitInfo);
        return reason is ApplicationExitInfoReason.Crash
            or ApplicationExitInfoReason.CrashNative
            or ApplicationExitInfoReason.Anr
            or ApplicationExitInfoReason.InitializationFailure
            or ApplicationExitInfoReason.Signaled
            or ApplicationExitInfoReason.LowMemory
            or ApplicationExitInfoReason.ExcessiveResourceUsage
            or ApplicationExitInfoReason.DependencyDied
            or ApplicationExitInfoReason.Other
            or ApplicationExitInfoReason.Unknown
            or ApplicationExitInfoReason.PermissionChange
            or ApplicationExitInfoReason.PackageStateChange;
    }

    private static ApplicationExitInfoReason GetExitReason(ApplicationExitInfo exitInfo)
    {
        return (ApplicationExitInfoReason)exitInfo.Reason;
    }

    private static string DescribeReason(ApplicationExitInfoReason reason)
    {
        return reason switch
        {
            ApplicationExitInfoReason.Anr => "ANR",
            ApplicationExitInfoReason.Crash => "Java crash",
            ApplicationExitInfoReason.CrashNative => "Native crash",
            ApplicationExitInfoReason.DependencyDied => "Dependency died",
            ApplicationExitInfoReason.ExcessiveResourceUsage => "Excessive resource usage",
            ApplicationExitInfoReason.ExitSelf => "Exit self",
            ApplicationExitInfoReason.InitializationFailure => "Initialization failure",
            ApplicationExitInfoReason.LowMemory => "Low memory kill",
            ApplicationExitInfoReason.Other => "Other system kill",
            ApplicationExitInfoReason.PackageStateChange => "Package state change",
            ApplicationExitInfoReason.PackageUpdated => "Package updated",
            ApplicationExitInfoReason.PermissionChange => "Permission change",
            ApplicationExitInfoReason.Signaled => "OS signal",
            ApplicationExitInfoReason.Unknown => "Unknown",
            ApplicationExitInfoReason.UserRequested => "User requested stop",
            ApplicationExitInfoReason.UserStopped => "User stopped",
            _ => $"Reason {(int)reason}"
        };
    }

    private static long ReadLastSeenExitTimestamp()
    {
        var path = GetArtifactPath(ExitInfoCursorFileName);
        if (!File.Exists(path))
            return 0;

        try
        {
            var content = File.ReadAllText(path).Trim();
            return long.TryParse(content, out var timestamp) ? timestamp : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void WriteLastSeenExitTimestamp(long timestamp)
    {
        var path = GetArtifactPath(ExitInfoCursorFileName);
        File.WriteAllText(path, timestamp.ToString());
    }

    private static string? RenderThrowable(Throwable? throwable)
    {
        if (throwable == null)
            return null;

        try
        {
            using var writer = new Java.IO.StringWriter();
            using var printWriter = new Java.IO.PrintWriter(writer);
            throwable.PrintStackTrace(printWriter);
            printWriter.Flush();
            return writer.ToString();
        }
        catch
        {
            return throwable.ToString();
        }
    }

    private static int? SafeThreadId(JThread? thread)
    {
        if (thread == null)
            return null;

        try
        {
            return checked((int)thread.Id);
        }
        catch
        {
            return null;
        }
    }

    private static T? TryReadArtifact<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteArtifact<T>(string fileName, T artifact)
    {
        lock (SyncRoot)
        {
            var path = GetArtifactPath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(artifact, JsonOptions);
            File.WriteAllText(path, json);
        }
    }

    private static void EnsureDiagnosticsDirectory()
    {
        Directory.CreateDirectory(GetDiagnosticsDirectory());
    }

    private static string GetDiagnosticsDirectory()
    {
        var root = _context?.FilesDir?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Path.GetTempPath(), "McpServerManager-Android");

        return Path.Combine(root, DiagnosticsDirectoryName, CrashDirectoryName);
    }

    private static string GetArtifactPath(string fileName) => Path.Combine(GetDiagnosticsDirectory(), fileName);

    private static string GetPendingArtifactPath(string fileName) => GetArtifactPath(fileName);

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var candidate = builder.ToString() + ch;
            if (Encoding.UTF8.GetByteCount(candidate) > maxBytes)
                break;
            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static void LogEarly(string message)
    {
        try
        {
            global::Android.Util.Log.Warn(LogTag, message);
        }
        catch
        {
            // Ignore logging failures during crash handling.
        }
    }

    private sealed class CrashUncaughtExceptionHandler : Java.Lang.Object, JThread.IUncaughtExceptionHandler
    {
        private readonly JThread.IUncaughtExceptionHandler? _innerHandler;

        public CrashUncaughtExceptionHandler(JThread.IUncaughtExceptionHandler? innerHandler)
        {
            _innerHandler = innerHandler;
        }

        public void UncaughtException(JThread? thread, Throwable? exception)
        {
            RecordJavaFatal(
                "Java.Lang.Thread.DefaultUncaughtExceptionHandler",
                thread,
                exception,
                "Default Android uncaught-exception handler captured a fatal thread termination.");

            _innerHandler?.UncaughtException(thread, exception);
        }
    }

    private sealed class BoundaryLease : IDisposable
    {
        private int _disposed;

        public BoundaryState State { get; }

        public BoundaryLease(string leaseId, string name, string? detail)
        {
            State = new BoundaryState
            {
                LeaseId = leaseId,
                Name = name,
                Detail = detail,
                StartedUtc = DateTimeOffset.UtcNow,
                ProcessId = global::Android.OS.Process.MyPid(),
                ThreadName = System.Threading.Thread.CurrentThread.Name ?? $"managed-{SysEnvironment.CurrentManagedThreadId}"
            };
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            TryClearActiveBoundary(State.LeaseId);
        }
    }

    private sealed class BoundaryState
    {
        public string LeaseId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Detail { get; init; }
        public DateTimeOffset StartedUtc { get; init; }
        public int ProcessId { get; init; }
        public string? ThreadName { get; init; }
    }

    private sealed class FatalCrashReport
    {
        public string Source { get; init; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; init; }
        public int ProcessId { get; init; }
        public string? ThreadName { get; init; }
        public int? ThreadId { get; init; }
        public string? ExceptionType { get; init; }
        public string? Message { get; init; }
        public string? Detail { get; init; }
        public string? StackTrace { get; init; }
        public string? JavaStackTrace { get; init; }
        public BoundaryState? ActiveBoundary { get; init; }
        public string? DeviceManufacturer { get; init; }
        public string? DeviceModel { get; init; }
        public string? AndroidRelease { get; init; }
        public string? PackageName { get; init; }
    }

    private sealed class ProcessExitInfoArtifact
    {
        public DateTimeOffset CapturedAtUtc { get; init; }
        public DateTimeOffset ExitTimestampUtc { get; init; }
        public string? ProcessName { get; init; }
        public int ProcessId { get; init; }
        public int Reason { get; init; }
        public string ReasonLabel { get; init; } = string.Empty;
        public int Status { get; init; }
        public int Importance { get; init; }
        public string? Description { get; init; }
        public long PssKb { get; init; }
        public long RssKb { get; init; }
        public string? TraceArtifactFile { get; init; }
        public string? ProcessStateSummary { get; init; }
    }

    private sealed class DiagnosticEventArtifact
    {
        public string Source { get; init; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; init; }
        public string? Message { get; init; }
        public string? Detail { get; init; }
        public string? ExceptionType { get; init; }
        public string? StackTrace { get; init; }
        public BoundaryState? ActiveBoundary { get; init; }
    }

    private sealed class CrashEvent
    {
        public string Source { get; init; } = string.Empty;
        public CrashEventKind Kind { get; init; }
        public LogLevel Severity { get; init; }
        public string? Message { get; init; }
        public string? Detail { get; init; }
        public string? ExceptionType { get; init; }
        public string? StackTrace { get; init; }
        public string? JavaStackTrace { get; init; }
        public string? ThreadName { get; init; }
        public int? ThreadId { get; init; }
        public string? StatusText { get; init; }
    }

    private sealed class RenderedCrashEvent
    {
        public string LogMessage { get; init; } = string.Empty;
        public string? StatusMessage { get; init; }
    }

    private enum CrashEventKind
    {
        Fatal,
        Diagnostic,
        RecoveredFatal,
        RecoveredExit,
        RecoveredBoundary
    }
}
