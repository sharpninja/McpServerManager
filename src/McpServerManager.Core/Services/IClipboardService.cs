using System.Threading.Tasks;

namespace McpServerManager.Core.Services
{
    public interface IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
