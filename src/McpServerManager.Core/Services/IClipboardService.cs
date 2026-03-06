using System.Threading.Tasks;

namespace McpServerManager.Core.Services
{
    public interface IClipboardService : McpServer.UI.Core.Services.IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
