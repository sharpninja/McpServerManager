using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Avalonia;
using Avalonia.Android;
using Avalonia.Threading;
using McpServerManager.Android.Services;

namespace McpServerManager.Android;

[Activity(
    Label = "Request Tracker",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        DeviceFormFactor.NotifyDisplayChanged();
    }

#pragma warning disable CS0618 // OnBackPressed is deprecated; used here to preserve expected Android back behavior in Avalonia phone views.
    public override void OnBackPressed()
    {
        bool handled;
        if (Dispatcher.UIThread.CheckAccess())
        {
            handled = AndroidBackNavigationService.TryHandleBack();
        }
        else
        {
            handled = Dispatcher.UIThread.InvokeAsync(AndroidBackNavigationService.TryHandleBack).GetAwaiter().GetResult();
        }

        if (handled)
            return;

        base.OnBackPressed();
    }
#pragma warning restore CS0618
}
