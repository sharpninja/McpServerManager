using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using McpServerManager.Core.Services;

namespace McpServerManager.Desktop.Services;

public class DesktopClipboardService : IClipboardService
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;

    public DesktopClipboardService(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
    }

    public async Task SetTextAsync(string text)
    {
        var mainWindow = _desktop.MainWindow;
        if (mainWindow?.Clipboard != null)
            await mainWindow.Clipboard.SetTextAsync(text);
    }
}
