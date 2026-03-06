using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class VoiceConversationViewModel
{
    private CqrsRelayCommand<bool>? _createSessionCommand;
    public CqrsRelayCommand<bool> CreateSessionCommand => _createSessionCommand ??= CqrsRelayFactory.Create(_dispatcher, CreateSessionAsync);

    private CqrsRelayCommand<bool>? _submitTurnCommand;
    public CqrsRelayCommand<bool> SubmitTurnCommand => _submitTurnCommand ??= CqrsRelayFactory.Create(_dispatcher, SubmitTurnAsync);

    private CqrsRelayCommand<bool>? _refreshTranscriptCommand;
    public CqrsRelayCommand<bool> RefreshTranscriptCommand => _refreshTranscriptCommand ??= CqrsRelayFactory.Create(_dispatcher, RefreshTranscriptAsync);

    private CqrsRelayCommand<bool>? _refreshStatusCommand;
    public CqrsRelayCommand<bool> RefreshStatusCommand => _refreshStatusCommand ??= CqrsRelayFactory.Create(_dispatcher, RefreshStatusAsync);

    private CqrsRelayCommand<bool>? _interruptCommand;
    public CqrsRelayCommand<bool> InterruptCommand => _interruptCommand ??= CqrsRelayFactory.Create(_dispatcher, InterruptAsync);

    private CqrsRelayCommand<bool>? _sendEscapeCommand;
    public CqrsRelayCommand<bool> SendEscapeCommand => _sendEscapeCommand ??= CqrsRelayFactory.Create(_dispatcher, SendEscapeAsync);

    private CqrsRelayCommand<bool>? _endSessionCommand;
    public CqrsRelayCommand<bool> EndSessionCommand => _endSessionCommand ??= CqrsRelayFactory.Create(_dispatcher, EndSessionAsync);

    private CqrsRelayCommand<bool>? _clearTurnInputCommand;
    public CqrsRelayCommand<bool> ClearTurnInputCommand => _clearTurnInputCommand ??= CqrsRelayFactory.Create(_dispatcher, ClearTurnInput);
}
