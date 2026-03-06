using System;
using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using CoreChatFileOpenResult = McpServerManager.Core.Services.ChatFileOpenResult;

namespace McpServerManager.Core.ViewModels;

public partial class ChatWindowViewModel : McpServer.UI.Core.ViewModels.ChatWindowViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public CqrsRelayCommand<CoreChatFileOpenResult> OpenAgentConfigCommand { get; }
    public CqrsRelayCommand<CoreChatFileOpenResult> OpenPromptTemplatesCommand { get; }

    public ChatWindowViewModel(
        CqrsDispatcher dispatcher,
        Func<string> getContext,
        string? initialModelFromConfig = null,
        Action<string?>? onModelChanged = null)
        : base(
            new ChatWindowServiceAdapter(dispatcher),
            getContext,
            initialModelFromConfig,
            onModelChanged,
            new AvaloniaUiDispatcherService())
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        OpenAgentConfigCommand = new CqrsRelayCommand<CoreChatFileOpenResult>(_dispatcher, () => new ChatOpenAgentConfigCommand());
        OpenPromptTemplatesCommand = new CqrsRelayCommand<CoreChatFileOpenResult>(_dispatcher, () => new ChatOpenPromptTemplatesCommand());
    }

    /// <summary>Parameterless constructor for design-time only.</summary>
    public ChatWindowViewModel() : this(LocalCqrsDispatcher.Instance, () => string.Empty, null, null)
    {
    }

    protected override void NotifySendCanExecuteChanged()
    {
        SendCommand.NotifyCanExecuteChanged();
    }
}
