// Legacy duplicate handler removed during CQRS remediation. Implementation consolidated in VoiceHandlers.cs
// This file kept as placeholder to avoid source delete in session; class intentionally commented out to prevent duplicate symbol.
#if false
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

internal sealed class CreateVoiceSessionCommandHandler : ICommandHandler<CreateVoiceSessionCommand, string>
{
    // removed
}
#endif
