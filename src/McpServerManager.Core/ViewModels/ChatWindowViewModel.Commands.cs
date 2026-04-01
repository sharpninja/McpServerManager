using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;

namespace McpServerManager.Core.ViewModels;

public partial class ChatWindowViewModel
{
    private CqrsRelayCommand<bool>? _populatePromptCommand;
    public CqrsRelayCommand<bool> PopulatePromptCommand => _populatePromptCommand ??=
        CqrsRelayFactory.Create<PromptTemplate?>(_dispatcher, prompt => PopulatePrompt(prompt));

    private CqrsRelayCommand<bool>? _submitPromptCommand;
    public CqrsRelayCommand<bool> SubmitPromptCommand => _submitPromptCommand ??=
        CqrsRelayFactory.Create<PromptTemplate?>(_dispatcher, prompt => SubmitPromptAsync(prompt));

    private CqrsRelayCommand<bool>? _sendCommand;
    public CqrsRelayCommand<bool> SendCommand => _sendCommand ??= CqrsRelayFactory.Create(_dispatcher, SendAsync, CanSend);
}
