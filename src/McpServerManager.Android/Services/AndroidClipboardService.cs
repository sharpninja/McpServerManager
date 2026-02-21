using System.Threading.Tasks;
using McpServerManager.Core.Services;

namespace McpServerManager.Android.Services;

/// <summary>Clipboard service for Android. Uses Android ClipboardManager.</summary>
public class AndroidClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var context = global::Android.App.Application.Context;
        var clipboard = (global::Android.Content.ClipboardManager?)
            context.GetSystemService(global::Android.Content.Context.ClipboardService);
        if (clipboard != null)
        {
            var clip = global::Android.Content.ClipData.NewPlainText("McpServerManager", text);
            clipboard.PrimaryClip = clip;
        }
        return Task.CompletedTask;
    }
}
