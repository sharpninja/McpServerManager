using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Global status sink. AddStatus appends to the observable history
/// and raises PropertyChanged for <see cref="Status"/> (the latest entry).
/// </summary>
public sealed class StatusViewModel : ObservableObject
{
    private static readonly Lazy<StatusViewModel> LazyInstance = new(() => new StatusViewModel());

    /// <summary>Singleton instance shared across the app.</summary>
    public static StatusViewModel Instance => LazyInstance.Value;

    private StatusViewModel() { }

    /// <summary>Full history of status messages.</summary>
    public ObservableCollection<string> Statuses { get; } = new();

    /// <summary>The most recent status message, or empty string if none.</summary>
    public string Status => Statuses.Count > 0 ? Statuses[^1] : string.Empty;

    /// <summary>Appends a status message and notifies bindings.</summary>
    public void AddStatus(string message)
    {
        Statuses.Add(message);
        OnPropertyChanged(nameof(Status));
    }
}
