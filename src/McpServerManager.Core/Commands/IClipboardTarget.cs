using System.Threading.Tasks;
using McpServerManager.Core.Models.Json;

namespace McpServerManager.Core.Commands;

/// <summary>
/// Clipboard copy operations.
/// </summary>
public interface IClipboardTarget
{
    Task CopyText(string text);
    Task CopyOriginalJson(UnifiedRequestEntry? entry);
}
