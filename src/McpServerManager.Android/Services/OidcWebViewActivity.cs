using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

/// <summary>
/// An Android Activity that hosts a WebView for in-app OIDC authentication.
/// Keeps the app in the foreground during sign-in so Android does not kill the process.
/// Shows a force-close dialog after <see cref="TimeoutSeconds"/> if authentication has not completed.
/// </summary>
[Activity(
    Label = "Sign In",
    Theme = "@style/MyTheme.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class OidcWebViewActivity : Activity
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("OidcWebViewActivity");
    private static WeakReference<OidcWebViewActivity>? _currentInstance;

    /// <summary>Seconds before the force-close dialog appears.</summary>
    private const int TimeoutSeconds = 60;

    private WebView? _webView;
    private Handler? _timeoutHandler;
    private Java.Lang.Runnable? _timeoutRunnable;
    private bool _dialogShown;

    /// <summary>
    /// Finishes the current OidcWebViewActivity instance if one is open.
    /// Safe to call from any thread.
    /// </summary>
    public static void FinishIfOpen()
    {
        if (_currentInstance != null && _currentInstance.TryGetTarget(out var activity))
        {
            _logger.LogInformation("FinishIfOpen: closing OidcWebViewActivity");
            activity.RunOnUiThread(() =>
            {
                try
                {
                    activity.Finish();
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning(ex, "FinishIfOpen: error closing activity");
                }
            });
        }
        else
        {
            _logger.LogDebug("FinishIfOpen: no active OidcWebViewActivity to close");
        }
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _logger.LogInformation("OidcWebViewActivity OnCreate");
        _currentInstance = new WeakReference<OidcWebViewActivity>(this);

        var url = Intent?.GetStringExtra("url");
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("OidcWebViewActivity started without a URL; finishing");
            Finish();
            return;
        }

        _webView = new WebView(this);
        _webView.Settings.JavaScriptEnabled = true;
        _webView.Settings.DomStorageEnabled = true;
        _webView.Settings.SetSupportMultipleWindows(false);
        _webView.Settings.UserAgentString = _webView.Settings.UserAgentString?
            .Replace("; wv)", ")"); // Remove "wv" marker so sites don't block WebView

        _webView.SetWebViewClient(new OidcWebViewClient());
        SetContentView(_webView);

        _logger.LogInformation("OidcWebViewActivity loading URL: {Url}", url);
        _webView.LoadUrl(url);

        ScheduleTimeoutDialog();
    }

    public override void OnBackPressed()
    {
        if (_webView != null && _webView.CanGoBack())
        {
            _webView.GoBack();
            return;
        }

        _logger.LogInformation("OidcWebViewActivity back pressed; finishing");
        base.OnBackPressed();
    }

    protected override void OnDestroy()
    {
        _logger.LogInformation("OidcWebViewActivity OnDestroy");
        CancelTimeout();

        if (_currentInstance != null && _currentInstance.TryGetTarget(out var instance) && instance == this)
        {
            _currentInstance = null;
        }

        if (_webView != null)
        {
            _webView.StopLoading();
            _webView.Destroy();
            _webView = null;
        }

        base.OnDestroy();
    }

    /// <summary>Schedules the force-close dialog after <see cref="TimeoutSeconds"/>.</summary>
    private void ScheduleTimeoutDialog()
    {
        _timeoutHandler = new Handler(Looper.MainLooper!);
        _timeoutRunnable = new Java.Lang.Runnable(ShowTimeoutDialog);
        _timeoutHandler.PostDelayed(_timeoutRunnable, TimeoutSeconds * 1000L);
        _logger.LogInformation("OidcWebViewActivity timeout scheduled in {Seconds}s", TimeoutSeconds);
    }

    private void CancelTimeout()
    {
        if (_timeoutHandler != null && _timeoutRunnable != null)
        {
            _timeoutHandler.RemoveCallbacks(_timeoutRunnable);
            _timeoutRunnable.Dispose();
            _timeoutRunnable = null;
            _timeoutHandler = null;
        }
    }

    private void ShowTimeoutDialog()
    {
        if (_dialogShown || IsFinishing || IsDestroyed)
            return;

        _dialogShown = true;
        _logger.LogWarning("OidcWebViewActivity sign-in timeout reached ({Seconds}s); showing force-close dialog", TimeoutSeconds);

        new AlertDialog.Builder(this)!
            .SetTitle("Sign-In Timeout")!
            .SetMessage("Authentication is taking longer than expected. Would you like to close and retry?")!
            .SetPositiveButton("Logout & Retry", (_, _) =>
            {
                _logger.LogInformation("User chose force-close from timeout dialog");
                Finish();
            })!
            .SetNegativeButton("Wait", (_, _) =>
            {
                _logger.LogInformation("User chose to continue waiting from timeout dialog");
                _dialogShown = false;
                ScheduleTimeoutDialog();
            })!
            .SetCancelable(false)!
            .Show();
    }

    /// <summary>
    /// WebViewClient that keeps all navigation within the WebView.
    /// </summary>
    private sealed class OidcWebViewClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
        {
            // Keep all navigation inside the WebView
            return false;
        }

        public override void OnReceivedError(WebView? view, IWebResourceRequest? request, WebResourceError? error)
        {
            base.OnReceivedError(view, request, error);
            if (request?.IsForMainFrame == true)
            {
                _logger.LogWarning(
                    "OidcWebView page error: {ErrorCode} {Description} for {Url}",
                    error?.ErrorCode,
                    error?.Description,
                    request.Url?.ToString());
            }
        }
    }
}
