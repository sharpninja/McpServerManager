using System.Threading.Tasks;
using System.Windows;
using McpServer.UI.Core.Services;

namespace McpServer.VsExtension.McpTodo;

internal sealed class VsixClipboardService : McpServer.UI.Core.Services.IClipboardService
{
    private readonly IUiDispatcherService _uiDispatcher;

    public VsixClipboardService(IUiDispatcherService uiDispatcher)
    {
        _uiDispatcher = uiDispatcher;
    }

    public Task SetTextAsync(string text)
    {
        if (_uiDispatcher.CheckAccess())
        {
            Clipboard.SetText(text ?? string.Empty);
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(() =>
        {
            Clipboard.SetText(text ?? string.Empty);
            return Task.CompletedTask;
        });
    }
}
