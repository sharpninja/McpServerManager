namespace McpServerManager.UI.Core.Commands;

/// <summary>
/// Target interface for refreshing health of the selected workspace connection (PLAN-VM-CQRS-REMEDIATION-001 remaining MainWindow thin).
/// </summary>
public interface IWorkspaceHealthTarget
{
    Task RefreshSelectedWorkspaceHealthAsync();
    void UpdateWorkspaceHealthIndicator(bool? isHealthy, string tooltip);
}
