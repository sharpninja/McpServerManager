using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class SettingsViewModel
{
    private CqrsRelayCommand<bool>? _saveFilterWordsCommand;
    public CqrsRelayCommand<bool> SaveFilterWordsCommand => _saveFilterWordsCommand ??= CqrsRelayFactory.Create(_dispatcher, SaveFilterWords);

    private CqrsRelayCommand<bool>? _revertFilterWordsCommand;
    public CqrsRelayCommand<bool> RevertFilterWordsCommand => _revertFilterWordsCommand ??= CqrsRelayFactory.Create(_dispatcher, RevertFilterWords);
}
