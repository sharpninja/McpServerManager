using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

/// <summary>App wrapper for UI.Core settings ViewModel.</summary>
public partial class SettingsViewModel : McpServer.UI.Core.ViewModels.SettingsViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public SettingsViewModel(
        CqrsDispatcher? dispatcher = null,
        McpServer.UI.Core.Services.ISpeechFilterService? speechFilterService = null)
        : base(speechFilterService ?? new SpeechFilterServiceAdapter())
    {
        _dispatcher = dispatcher ?? LocalCqrsDispatcher.Instance;
    }
}
