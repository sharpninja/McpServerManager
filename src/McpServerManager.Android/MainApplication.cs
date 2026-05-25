using Android.App;
using Android.Runtime;
using Avalonia.Android;

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.RecordAudio)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]
[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE_MICROPHONE")]
[assembly: UsesPermission(Android.Manifest.Permission.PostNotifications)]

namespace McpServerManager.Android;

#if DEBUG
[Application(UsesCleartextTraffic = true, NetworkSecurityConfig = "@xml/network_security_config")]
#else
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
#endif
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(nint handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        void Core()
        {
            base.OnCreate();
            Services.AndroidCrashDiagnostics.Initialize(this);
        }

        Services.AndroidCrashDiagnostics.ExecuteFatal(
            "MainApplication.OnCreate",
            Core,
            "Android application crashed while installing early crash diagnostics.");
    }
}
