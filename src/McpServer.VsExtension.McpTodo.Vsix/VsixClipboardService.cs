using System.Threading.Tasks;
using System.Windows;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.VsExtension.McpTodo;

internal sealed class VsixClipboardService : McpServerManager.UI.Core.Services.IClipboardService
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
