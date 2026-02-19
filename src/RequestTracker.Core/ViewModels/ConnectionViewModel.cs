using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RequestTracker.Core.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _host = "192.168.1.100";

    [ObservableProperty]
    private string _port = "5000";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isConnecting;

    /// <summary>Raised when the user taps Connect with a valid URL.</summary>
    public event Action<string>? Connected;

    [RelayCommand]
    private void Connect()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(Host))
        {
            ErrorMessage = "Host is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Port) || !int.TryParse(Port.Trim(), out var portNumber) || portNumber < 1 || portNumber > 65535)
        {
            ErrorMessage = "Port must be between 1 and 65535.";
            return;
        }

        var url = $"http://{Host.Trim()}:{portNumber}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            ErrorMessage = "Invalid host or port.";
            return;
        }

        IsConnecting = true;
        Connected?.Invoke(url);
    }
}
