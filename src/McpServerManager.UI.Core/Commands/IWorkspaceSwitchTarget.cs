using System.Threading.Tasks;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Workspace connection switch orchestration via CQRS.
/// </summary>
public interface IWorkspaceSwitchTarget
{
    Task SwitchWorkspaceConnectionAsync(WorkspaceConnectionOption option);
}
