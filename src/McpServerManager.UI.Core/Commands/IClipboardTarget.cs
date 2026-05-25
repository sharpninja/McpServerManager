using System.Threading.Tasks;
using McpServerManager.UI.Core.Models.Json;

namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Clipboard copy operations.
/// </summary>
public interface IClipboardTarget
{
    Task CopyText(string text);
    Task CopyOriginalJson(UnifiedSessionTurn? entry);
}

