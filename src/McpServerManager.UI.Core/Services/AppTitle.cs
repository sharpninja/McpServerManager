using System.Reflection;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Builds user-visible app titles with a normalized SemVer suffix.
/// </summary>
public static class AppTitle
{
    public static string Build(string appName, Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"{appName.Trim()} v{ResolveVersion(assembly)}";
    }

    public static string ResolveVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? typeof(AppTitle).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString()
            : informationalVersion;

        return NormalizeVersion(version);
    }

    public static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "unknown";

        version = version.Trim();

        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex > 0)
            version = version[..plusIndex];

        var markerIndex = version.IndexOf(".Sha", StringComparison.OrdinalIgnoreCase);
        return markerIndex > 0 ? version[..markerIndex] : version;
    }
}
