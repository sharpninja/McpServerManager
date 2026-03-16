using System.Threading.Tasks;
using System.Windows;

namespace McpServer.VsExtension.McpTodo;

internal sealed class VsixClipboardService : McpServer.UI.Core.Services.IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var dispatcher = Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
        {
            Clipboard.SetText(text ?? string.Empty);
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() => Clipboard.SetText(text ?? string.Empty)).Task;
    }
}
