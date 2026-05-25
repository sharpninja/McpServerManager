namespace McpServerManager.UI.Core.Services;

/// <summary>Host-provided API abstraction for server configuration management.</summary>
public interface IConfigurationApiClient
{
    /// <summary>Gets the effective flattened configuration as section:key pairs.</summary>
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(CancellationToken cancellationToken = default);

    /// <summary>Patches the configuration with the supplied key/value pairs.</summary>
    Task<IReadOnlyDictionary<string, string>> PatchValuesAsync(
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);
}
