using System.Threading.Tasks;

namespace RequestTracker.Core.Services
{
    public interface IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
