using System.Threading;
using System.Threading.Tasks;
using McpServerManager.UI.Core.Messages;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Publishes an exported requirements wiki document to an external target
/// (PLAN-REQSDESKTOP-001, FR-REQS-PUSH-001). Hosts provide a concrete adapter (GitHub / Azure DevOps);
/// UI.Core ships a no-op default so the CQRS graph resolves without host wiring.
/// </summary>
public interface IRequirementsWikiPublisher
{
    /// <summary>Publishes the generated wiki content to the given target.</summary>
    /// <param name="content">The generated document bytes.</param>
    /// <param name="contentType">The document content type, when known.</param>
    /// <param name="target">Where to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The push result.</returns>
    Task<WikiPushResult> PublishAsync(
        byte[] content,
        string? contentType,
        WikiPushTarget target,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op <see cref="IRequirementsWikiPublisher"/> used when no host adapter is registered. Returns a
/// failure indicating the publisher is not configured, so the UI can surface actionable status.
/// </summary>
public sealed class NoOpRequirementsWikiPublisher : IRequirementsWikiPublisher
{
    /// <inheritdoc />
    public Task<WikiPushResult> PublishAsync(
        byte[] content,
        string? contentType,
        WikiPushTarget target,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new WikiPushResult(
            Success: false,
            Error: $"No wiki publisher is configured for {target}. Register an IRequirementsWikiPublisher adapter.",
            Location: null));
}
