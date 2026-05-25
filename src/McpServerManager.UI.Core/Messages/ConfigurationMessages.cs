using McpServer.Cqrs;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Query to retrieve the current effective flattened configuration.</summary>
public sealed record GetConfigurationValuesQuery : IQuery<IReadOnlyDictionary<string, string>>;

/// <summary>Command to patch one or more configuration keys.</summary>
public sealed record PatchConfigurationValuesCommand(
    IReadOnlyDictionary<string, string?> Values
) : ICommand<IReadOnlyDictionary<string, string>>;
