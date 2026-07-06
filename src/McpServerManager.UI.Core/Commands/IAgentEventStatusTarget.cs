namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Target for status text updates produced by the agent event listener coordinator.
/// </summary>
public interface IAgentEventStatusTarget
{
    void SetAgentEventStatus(string message);
}
