using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Avalonia.Android;
using McpServerManager.UI.Core.Services;
using McpServerManager.Android.Services;

namespace McpServerManager.Android;

/// <summary>Android application class for Avalonia 12 initialization.</summary>
[Application]
public class McpServerManagerApplication : AvaloniaAndroidApplication<App>;

[Activity(
    Label = "MCP Server Manager",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        void Core()
        {
            global::Android.Util.Log.Info("McpSM", "[MainActivity] OnCreate entered");
            base.OnCreate(savedInstanceState);
            AndroidActivityHost.Register(this);
            global::Android.Util.Log.Info("McpSM", "[MainActivity] OnCreate completed");
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "MainActivity.OnCreate",
            Core,
            "Main Android activity crashed during creation.");
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        void Core()
        {
            base.OnConfigurationChanged(newConfig);
            DeviceFormFactor.NotifyDisplayChanged();
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "MainActivity.OnConfigurationChanged",
            Core,
            "Main Android activity crashed while reacting to a configuration change.");
    }

    protected override void OnResume()
    {
        void Core()
        {
            base.OnResume();
            AndroidActivityHost.Register(this);
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "MainActivity.OnResume",
            Core,
            "Main Android activity crashed while resuming.");
    }

    public override void OnRequestPermissionsResult(int requestCode, string[]? permissions, Permission[] grantResults)
    {
        void Core()
        {
            AndroidActivityHost.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions ?? [], grantResults);
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "MainActivity.OnRequestPermissionsResult",
            Core,
            "Main Android activity crashed while dispatching runtime-permission results.");
    }

#pragma warning disable CS0618 // OnBackPressed is deprecated; used here to preserve expected Android back behavior in Avalonia phone views.
    public override void OnBackPressed()
    {
        bool Core()
        {
            var handled = UiDispatcherHost.CheckAccess()
                ? AndroidBackNavigationService.TryHandleBack()
                : UiDispatcherHost.InvokeAsync(AndroidBackNavigationService.TryHandleBack).GetAwaiter().GetResult();

            if (handled)
                return true;

            base.OnBackPressed();
            return false;
        }

        AndroidCrashDiagnostics.ExecuteFatal(
            "MainActivity.OnBackPressed",
            Core,
            "Main Android activity crashed while processing a back-navigation request.");
    }
#pragma warning restore CS0618
}
