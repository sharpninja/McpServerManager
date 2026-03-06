using McpServerManager.Services;

namespace McpServerManager.ViewModels;

public partial class MainWindowViewModel : McpServerManager.Core.ViewModels.MainWindowViewModel
{
    public MainWindowViewModel()
        : base(new ClipboardService())
    {
    }

    public MainWindowViewModel(IClipboardService clipboardService)
        : base(clipboardService)
    {
    }
}
