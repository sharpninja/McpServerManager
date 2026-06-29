using Microsoft.AspNetCore.Components.WebView;

namespace McpServerManager.Web.Hybrid;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
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
