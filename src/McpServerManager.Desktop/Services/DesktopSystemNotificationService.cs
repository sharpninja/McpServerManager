using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Desktop.Services;

/// <summary>
/// Best-effort desktop system notification service for actionable agent events.
/// </summary>
public sealed class DesktopSystemNotificationService : ISystemNotificationService
{
    private const string ToastAppId = "RequestTracker.McpServerManager.Desktop";
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("DesktopSystemNotificationService");

    /// <inheritdoc />
    public Task NotifyAgentEventAsync(
        McpIncomingChangeEvent changeEvent,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeEvent);

        if (cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(message))
            return Task.CompletedTask;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Task.CompletedTask;

        try
        {
            if (!TryShowWindowsToast("RequestTracker", message))
                _logger.LogDebug("[SystemNotification] Windows toast unavailable; continuing without toast.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SystemNotification] Windows toast failed; continuing without toast.");
        }

        return Task.CompletedTask;
    }

    private static bool TryShowWindowsToast(string title, string message)
    {
        var toastManagerType = Type.GetType("Windows.UI.Notifications.ToastNotificationManager, Windows, ContentType=WindowsRuntime");
        var toastType = Type.GetType("Windows.UI.Notifications.ToastNotification, Windows, ContentType=WindowsRuntime");
        var xmlDocumentType = Type.GetType("Windows.Data.Xml.Dom.XmlDocument, Windows, ContentType=WindowsRuntime");
        if (toastManagerType is null || toastType is null || xmlDocumentType is null)
            return false;

        var xmlDocument = Activator.CreateInstance(xmlDocumentType);
        var loadXml = xmlDocumentType.GetMethod("LoadXml", [typeof(string)]);
        if (xmlDocument is null || loadXml is null)
            return false;

        loadXml.Invoke(xmlDocument, [BuildToastXml(title, message)]);

        var toast = Activator.CreateInstance(toastType, xmlDocument);
        if (toast is null)
            return false;

        object? notifier =
            toastManagerType.GetMethod("CreateToastNotifier", Type.EmptyTypes)?.Invoke(null, null) ??
            toastManagerType.GetMethod("CreateToastNotifier", [typeof(string)])?.Invoke(null, [ToastAppId]);

        if (notifier is null)
            return false;

        var showMethod = notifier.GetType().GetMethod("Show");
        if (showMethod is null)
            return false;

        showMethod.Invoke(notifier, [toast]);
        return true;
    }

    private static string BuildToastXml(string title, string message)
    {
        var safeTitle = SecurityElement.Escape(title) ?? string.Empty;
        var safeMessage = SecurityElement.Escape(message) ?? string.Empty;

        return
            "<toast><visual><binding template=\"ToastGeneric\">" +
            $"<text>{safeTitle}</text><text>{safeMessage}</text>" +
            "</binding></visual></toast>";
    }
}
