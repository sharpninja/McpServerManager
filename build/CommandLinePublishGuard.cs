using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace McpServerManager.Build;

/// <summary>
/// director is compiled against stable System.CommandLine 2.0.12.
/// Assembly 2.0.0.0 is the 2.0.0-beta4 contract, whose AddCommand method
/// stable 2.0 removed. A reused publish directory can keep that older DLL
/// because the SDK copy skips a destination that is newer than the package.
/// </summary>
public static class CommandLinePublishGuard
{
    public static readonly Version MinimumStableVersion = new(2, 0, 3, 0);

    public static bool IsSupportedVersion(Version? version)
        => version is not null && version >= MinimumStableVersion;

    public static void EnsureCompatible(string systemCommandLineDllPath)
    {
        if (string.IsNullOrWhiteSpace(systemCommandLineDllPath) || !File.Exists(systemCommandLineDllPath))
        {
            throw new InvalidOperationException(
                $"System.CommandLine.dll was not found at '{systemCommandLineDllPath}'.");
        }

        var version = AssemblyName.GetAssemblyName(systemCommandLineDllPath).Version;
        if (!IsSupportedVersion(version) || !DeclaresSetAction(systemCommandLineDllPath))
        {
            throw new InvalidOperationException(
                $"Published System.CommandLine {version} at '{systemCommandLineDllPath}' is not a stable 2.0 build with SetAction. " +
                "director requires System.CommandLine 2.0.12 or another stable 2.0.3+ package. " +
                "Clear the publish directory before publish so a beta or stale DLL cannot be kept.");
        }
    }

    private static bool DeclaresSetAction(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (!string.Equals(reader.GetString(type.Namespace), "System.CommandLine", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(reader.GetString(type.Name), "Command", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (string.Equals(reader.GetString(method.Name), "SetAction", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
