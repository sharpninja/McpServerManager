using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class ConnectionViewModel
{
    private CqrsRelayCommand<bool>? _scanQrCodeCommand;
    public CqrsRelayCommand<bool> ScanQrCodeCommand => _scanQrCodeCommand ??= CqrsRelayFactory.Create(_dispatcher, ScanQrCodeAsync);

    private CqrsRelayCommand<bool>? _logoutAndRetryCommand;
    public CqrsRelayCommand<bool> LogoutAndRetryCommand => _logoutAndRetryCommand ??= CqrsRelayFactory.Create(_dispatcher, LogoutAndRetryAsync);

    private CqrsRelayCommand<bool>? _logoutCommand;
    public CqrsRelayCommand<bool> LogoutCommand => _logoutCommand ??= CqrsRelayFactory.Create(_dispatcher, LogoutAsync);

    private CqrsRelayCommand<bool>? _cancelConnectCommand;
    public CqrsRelayCommand<bool> CancelConnectCommand => _cancelConnectCommand ??= CqrsRelayFactory.Create(_dispatcher, CancelConnect);

    private CqrsRelayCommand<bool>? _connectCommand;
    public CqrsRelayCommand<bool> ConnectCommand => _connectCommand ??= CqrsRelayFactory.Create(_dispatcher, ConnectAsync);

    private CqrsRelayCommand<bool>? _openOidcVerificationUrlCommand;
    public CqrsRelayCommand<bool> OpenOidcVerificationUrlCommand => _openOidcVerificationUrlCommand ??= CqrsRelayFactory.Create(_dispatcher, OpenOidcVerificationUrl);
}
