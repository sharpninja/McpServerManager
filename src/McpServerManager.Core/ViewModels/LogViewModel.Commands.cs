using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class LogViewModel
{
    private CqrsRelayCommand<bool>? _copySelectedCommand;
    public CqrsRelayCommand<bool> CopySelectedCommand => _copySelectedCommand ??= CqrsRelayFactory.Create(_dispatcher, CopySelectedAsync);

    private CqrsRelayCommand<bool>? _copyAllCommand;
    public CqrsRelayCommand<bool> CopyAllCommand => _copyAllCommand ??= CqrsRelayFactory.Create(_dispatcher, CopyAllAsync);

    private CqrsRelayCommand<bool>? _clearLogCommand;
    public CqrsRelayCommand<bool> ClearLogCommand => _clearLogCommand ??= CqrsRelayFactory.Create(_dispatcher, ClearLog);
}
