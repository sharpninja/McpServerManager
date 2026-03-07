using System.Threading.Tasks;

namespace McpServerManager.Core.Commands;

/// <summary>
/// Target interface for TODO Copilot prompt commands.
/// Implemented by the ViewModel layer to handle streaming prompt generation.
/// </summary>
public interface ITodoCopilotTarget : McpServer.UI.Core.Commands.ITodoCopilotTarget
{
    new Task CopilotStatusAsync();
    new Task CopilotPlanAsync();
    new Task CopilotImplementAsync();
}
