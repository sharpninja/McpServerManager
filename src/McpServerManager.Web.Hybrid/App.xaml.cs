using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Hybrid;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = AppTitle.Build("MCP Server Manager", typeof(App).Assembly) };
    }
}
