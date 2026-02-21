using System.Threading.Tasks;

namespace McpServerManager.Services
{
    public interface IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
