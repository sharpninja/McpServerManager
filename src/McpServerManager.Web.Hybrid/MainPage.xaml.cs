using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;
using McpServerManager.Web.Hybrid.Components;

namespace McpServerManager.Web.Hybrid;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(HybridRoot)
        });
        blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
    }

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(250);
            nativeSplash.IsVisible = false;
        });
    }
}
