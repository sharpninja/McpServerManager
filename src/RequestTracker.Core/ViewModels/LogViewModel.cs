using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Services;

namespace RequestTracker.Core.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private readonly List<LogEntry> _allEntries = new();

    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = new();
    [ObservableProperty] private LogEntry? _selectedEntry;
    [ObservableProperty] private string _statusText = "Ready";

    public static string[] LevelFilterOptions { get; } =
        ["All", "Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    [ObservableProperty] private string _selectedLevelFilter = "Information";
    [ObservableProperty] private bool _isPaused;

    public string PauseButtonText => IsPaused ? "▶ Resume" : "⏸ Pause";

    /// <summary>Raised when a new entry is added so the view can auto-scroll.</summary>
    public event Action? NewEntryAdded;

    public LogViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        // Load existing entries
        foreach (var entry in AppLogService.Instance.Entries)
            _allEntries.Add(entry);
        ApplyFilter();
        if (LogEntries.Count > 0)
            SelectedEntry = LogEntries[^1];

        // Subscribe to new entries
        AppLogService.Instance.EntryAdded += OnEntryAdded;
    }

    partial void OnSelectedLevelFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        LogLevel? minLevel = SelectedLevelFilter switch
        {
            "Trace" => LogLevel.Trace,
            "Debug" => LogLevel.Debug,
            "Information" => LogLevel.Information,
            "Warning" => LogLevel.Warning,
            "Error" => LogLevel.Error,
            "Critical" => LogLevel.Critical,
            _ => null
        };

        LogEntries.Clear();
        foreach (var entry in _allEntries)
        {
            if (minLevel == null || entry.Level >= minLevel.Value)
                LogEntries.Add(entry);
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText = SelectedLevelFilter == "All"
            ? $"{LogEntries.Count} log entries"
            : $"{LogEntries.Count} of {_allEntries.Count} log entries (≥{SelectedLevelFilter})";
    }

    private bool PassesFilter(LogEntry entry)
    {
        if (SelectedLevelFilter == "All") return true;
        LogLevel minLevel = SelectedLevelFilter switch
        {
            "Trace" => LogLevel.Trace,
            "Debug" => LogLevel.Debug,
            "Information" => LogLevel.Information,
            "Warning" => LogLevel.Warning,
            "Error" => LogLevel.Error,
            "Critical" => LogLevel.Critical,
            _ => LogLevel.Trace
        };
        return entry.Level >= minLevel;
    }

    private readonly List<LogEntry> _pauseBuffer = new();

    private void OnEntryAdded(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _allEntries.Add(entry);
            if (IsPaused)
            {
                _pauseBuffer.Add(entry);
                UpdateStatus();
                return;
            }
            if (PassesFilter(entry))
            {
                LogEntries.Add(entry);
                SelectedEntry = entry;
                NewEntryAdded?.Invoke();
            }
            UpdateStatus();
        });
    }

    partial void OnIsPausedChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(PauseButtonText));
        if (!newValue && _pauseBuffer.Count > 0)
        {
            // Flush buffered entries
            foreach (var entry in _pauseBuffer)
            {
                if (PassesFilter(entry))
                    LogEntries.Add(entry);
            }
            if (LogEntries.Count > 0)
            {
                SelectedEntry = LogEntries[^1];
                NewEntryAdded?.Invoke();
            }
            _pauseBuffer.Clear();
            UpdateStatus();
        }
    }

    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        if (SelectedEntry != null)
        {
            await _clipboardService.SetTextAsync(SelectedEntry.Display);
            StatusText = "Copied to clipboard";
        }
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (LogEntries.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var entry in LogEntries)
            sb.AppendLine(entry.Display);
        await _clipboardService.SetTextAsync(sb.ToString());
        StatusText = $"Copied {LogEntries.Count} entries to clipboard";
    }

    [RelayCommand]
    private void ClearLog()
    {
        AppLogService.Instance.Clear();
        _allEntries.Clear();
        LogEntries.Clear();
        StatusText = "Log cleared";
    }
}
