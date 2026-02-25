using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

public sealed record ChatFileOpenResult(bool Opened, string? FilePath = null, string? ErrorMessage = null);

public sealed record ChatSendRequest(string UserMessage, string ContextSummary, string? Model);

/// <summary>Reads and parses prompt templates used by the chat UI.</summary>
public interface IChatPromptTemplateService
{
    IReadOnlyList<PromptTemplate> GetPromptTemplates();
}

/// <summary>Ensures chat-related local files exist and opens them in the platform editor.</summary>
public interface IChatConfigFilesService
{
    ChatFileOpenResult OpenAgentConfigInEditor();
    ChatFileOpenResult OpenPromptTemplatesInEditor();
}

/// <summary>Discovers available AI models from the configured backend.</summary>
public interface IChatModelDiscoveryService
{
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Owns the backend chat send call contract (streaming/cancellation capable).</summary>
public interface IChatSendOrchestrationService
{
    Task<string> SendMessageAsync(ChatSendRequest request, IProgress<string>? contentProgress = null, CancellationToken cancellationToken = default);
}

public sealed class LocalChatPromptTemplateService : IChatPromptTemplateService
{
    public IReadOnlyList<PromptTemplate> GetPromptTemplates() => PromptTemplatesIo.GetPrompts();
}

public sealed class LocalChatConfigFilesService : IChatConfigFilesService
{
    public ChatFileOpenResult OpenAgentConfigInEditor()
    {
        AgentConfigIo.EnsureExists();
        return OpenFileInDefaultEditor(AgentConfigIo.GetFilePath());
    }

    public ChatFileOpenResult OpenPromptTemplatesInEditor()
    {
        PromptTemplatesIo.EnsureExists();
        return OpenFileInDefaultEditor(PromptTemplatesIo.GetFilePath());
    }

    private static ChatFileOpenResult OpenFileInDefaultEditor(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new ChatFileOpenResult(false, null, "Path is empty.");

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return new ChatFileOpenResult(false, fullPath, "File does not exist.");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{fullPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = fullPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }

            return new ChatFileOpenResult(true, fullPath, null);
        }
        catch (Exception ex)
        {
            return new ChatFileOpenResult(false, fullPath, ex.Message);
        }
    }
}

public sealed class OllamaChatModelDiscoveryService : IChatModelDiscoveryService
{
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => await OllamaLogAgentService.GetAvailableModelsAsync(null, cancellationToken).ConfigureAwait(true);
}

public sealed class LogAgentChatSendOrchestrationService : IChatSendOrchestrationService
{
    private readonly ILogAgentService _agentService;

    public LogAgentChatSendOrchestrationService(ILogAgentService agentService)
        => _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));

    public Task<string> SendMessageAsync(ChatSendRequest request, IProgress<string>? contentProgress = null, CancellationToken cancellationToken = default)
        => _agentService.SendMessageAsync(
            request.UserMessage,
            request.ContextSummary,
            request.Model,
            contentProgress,
            cancellationToken);
}

