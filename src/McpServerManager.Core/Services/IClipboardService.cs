using System.Threading.Tasks;

namespace McpServerManager.Core.Services
{
    public interface IClipboardService : McpServerManager.UI.Core.Services.IClipboardService
    {
        new Task SetTextAsync(string text);
    }
}
