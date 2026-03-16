using McpServer.UI.Core.ViewModels;
using Xunit;

namespace McpServer.UI.Core.Tests.ViewModels;

public sealed class MainWindowViewModelVersionTests
{
    [Theory]
    [InlineData("0.5.1+Branch.main.Sha.abc123", "0.5.1")]
    [InlineData("0.5.1-30+Branch.main.Sha.abc123", "0.5.1-30")]
    [InlineData("0.5.1.Sha.abc123", "0.5.1")]
    [InlineData("0.5.1", "0.5.1")]
    public void NormalizeAppVersion_TruncatesBuildMetadataAndShaSuffix(string rawVersion, string expected)
    {
        var actual = MainWindowViewModel.NormalizeAppVersion(rawVersion);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeAppVersion_ReturnsUnknownForMissingVersion()
    {
        Assert.Equal("unknown", MainWindowViewModel.NormalizeAppVersion(null));
        Assert.Equal("unknown", MainWindowViewModel.NormalizeAppVersion(string.Empty));
        Assert.Equal("unknown", MainWindowViewModel.NormalizeAppVersion("   "));
    }
}
