using Android.App;

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
public class MainApplication : global::Android.App.Application
{
    public MainApplication(nint handle, global::Android.Runtime.JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }
}
