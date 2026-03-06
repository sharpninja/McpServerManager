namespace McpServerManager.Core.Commands;

/// <summary>
/// Configuration file opening operations.
/// </summary>
public interface IConfigTarget
{
    void OpenAgentConfig();
    void OpenPromptTemplates();
}

