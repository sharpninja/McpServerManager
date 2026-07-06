using FluentAssertions;
using Xunit;

namespace McpServerManager.Core.Tests.Architecture;

public sealed class TestDoublePolicyTests
{
    [Fact]
    public void TestSources_DoNotUseBannedMockingLibrary()
    {
        var root = FindWorkspaceRoot();
        var bannedUsing = "using " + "Mo" + "q;";
        var bannedNamespace = "Mo" + "q.";
        var bannedNewMock = "new " + "Mo" + "ck<";
        var bannedMockType = "Mo" + "ck<";
        var matches = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.StartsWith(Path.Combine(root, "tests"), StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(item =>
                item.Text.Contains(bannedUsing, StringComparison.Ordinal) ||
                item.Text.Contains(bannedNamespace, StringComparison.Ordinal) ||
                item.Text.Contains(bannedNewMock, StringComparison.Ordinal) ||
                item.Text.Contains(bannedMockType, StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(root, item.Path))
            .ToArray();

        matches.Should().BeEmpty("NSubstitute is the required test-double library");
    }

    [Fact]
    public void TestProjects_DoNotReferenceBannedMockingPackage()
    {
        var root = FindWorkspaceRoot();
        var bannedPackage = string.Concat("Include=\"", "Mo", "q", "\"");
        var matches = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(item => item.Text.Contains(bannedPackage, StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.GetRelativePath(root, item.Path))
            .ToArray();

        matches.Should().BeEmpty("the banned mocking package must not be referenced by test projects");
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "McpServerManager.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        return "F:/GitHub/McpServerManager";
    }
}
