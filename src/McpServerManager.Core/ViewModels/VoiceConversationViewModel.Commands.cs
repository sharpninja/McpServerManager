using CommunityToolkit.Mvvm.Input;

namespace McpServerManager.Core.ViewModels;

public partial class VoiceConversationViewModel
{
    private IAsyncRelayCommand? _createSessionCommand;
    public IAsyncRelayCommand CreateSessionCommand => _createSessionCommand ??= new AsyncRelayCommand(CreateSessionAsync);

    private IAsyncRelayCommand? _submitTurnCommand;
    public IAsyncRelayCommand SubmitTurnCommand => _submitTurnCommand ??= new AsyncRelayCommand(SubmitTurnAsync);

    private IAsyncRelayCommand? _refreshTranscriptCommand;
    public IAsyncRelayCommand RefreshTranscriptCommand => _refreshTranscriptCommand ??= new AsyncRelayCommand(RefreshTranscriptAsync);

    private IAsyncRelayCommand? _refreshStatusCommand;
    public IAsyncRelayCommand RefreshStatusCommand => _refreshStatusCommand ??= new AsyncRelayCommand(RefreshStatusAsync);

    private IAsyncRelayCommand? _interruptCommand;
    public IAsyncRelayCommand InterruptCommand => _interruptCommand ??= new AsyncRelayCommand(InterruptAsync);

    private IAsyncRelayCommand? _sendEscapeCommand;
    public IAsyncRelayCommand SendEscapeCommand => _sendEscapeCommand ??= new AsyncRelayCommand(SendEscapeAsync);

    private IAsyncRelayCommand? _endSessionCommand;
    public IAsyncRelayCommand EndSessionCommand => _endSessionCommand ??= new AsyncRelayCommand(EndSessionAsync);

    private IRelayCommand? _clearTurnInputCommand;
    public IRelayCommand ClearTurnInputCommand => _clearTurnInputCommand ??= new RelayCommand(ClearTurnInput);
}
