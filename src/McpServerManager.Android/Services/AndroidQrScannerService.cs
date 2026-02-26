using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.CodeScanner;

namespace McpServerManager.Android.Services;

/// <summary>
/// Launches Google's Code Scanner to scan a QR code and return the decoded text.
/// Uses GMS Code Scanner which provides its own camera UI — no CAMERA permission needed.
/// </summary>
public static class AndroidQrScannerService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("QrScanner");

    /// <summary>
    /// Launch the Google Code Scanner, wait for a QR code, and return its raw text value.
    /// Returns null if cancelled or on error.
    /// </summary>
    public static Task<string?> ScanQrCodeAsync()
    {
        var activity = AndroidActivityHost.TryGetCurrentActivity();
        if (activity == null)
        {
            _logger.LogWarning("No current activity available for QR scanning");
            return Task.FromResult<string?>(null);
        }

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var options = new GmsBarcodeScannerOptions.Builder()
                .SetBarcodeFormats(Barcode.FormatQrCode)
                .Build();

            var scanner = GmsBarcodeScanning.GetClient(activity, options);
            scanner.StartScan()
                .AddOnSuccessListener(new ScanSuccessListener(tcs, _logger))
                .AddOnFailureListener(new ScanFailureListener(tcs, _logger))
                .AddOnCanceledListener(new ScanCancelledListener(tcs, _logger));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start QR scanner");
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    private sealed class ScanSuccessListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnSuccessListener
    {
        private readonly TaskCompletionSource<string?> _tcs;
        private readonly ILogger _logger;

        public ScanSuccessListener(TaskCompletionSource<string?> tcs, ILogger logger)
        {
            _tcs = tcs;
            _logger = logger;
        }

        public void OnSuccess(Java.Lang.Object? result)
        {
            if (result is Barcode barcode)
            {
                var value = barcode.RawValue;
                _logger.LogInformation("QR code scanned: {Value}", value);
                _tcs.TrySetResult(value);
            }
            else
            {
                _logger.LogWarning("QR scan returned unexpected result type");
                _tcs.TrySetResult(null);
            }
        }
    }

    private sealed class ScanFailureListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnFailureListener
    {
        private readonly TaskCompletionSource<string?> _tcs;
        private readonly ILogger _logger;

        public ScanFailureListener(TaskCompletionSource<string?> tcs, ILogger logger)
        {
            _tcs = tcs;
            _logger = logger;
        }

        public void OnFailure(Java.Lang.Exception e)
        {
            _logger.LogWarning("QR scan failed: {Message}", e.Message);
            _tcs.TrySetResult(null);
        }
    }

    private sealed class ScanCancelledListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCanceledListener
    {
        private readonly TaskCompletionSource<string?> _tcs;
        private readonly ILogger _logger;

        public ScanCancelledListener(TaskCompletionSource<string?> tcs, ILogger logger)
        {
            _tcs = tcs;
            _logger = logger;
        }

        public void OnCanceled()
        {
            _logger.LogInformation("QR scan cancelled by user");
            _tcs.TrySetResult(null);
        }
    }
}
