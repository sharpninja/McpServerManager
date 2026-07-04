using System.Threading.Tasks;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Target for LoadWorkspaceConnections CQRS command (per PLAN-VM-CQRS-REMEDIATION-001 C4).
/// Allows handler to delegate to VM internal without VM entry holding logic.
/// </summary>
public interface ILoadWorkspaceConnectionsTarget
{
    Task LoadWorkspaceConnectionsAsync(WorkspaceConnectionOption? preferredSelection, string preferredBaseUrl, bool suppressStatusFailure);
}
