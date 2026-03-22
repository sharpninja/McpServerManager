using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Hosting;
using NSubstitute;
using Xunit;

namespace McpServer.UI.Core.Tests;

public sealed class McpHostOptionsTests
{
    [Fact]
    public void Constructor_DefaultsToSingletonLifetime_AndOptionalCommandTarget()
    {
        var options = new McpHostOptions();

        Assert.Equal(McpHostLifetimeStrategy.Singleton, options.Lifetime);
        Assert.Null(options.CommandTarget);
        Assert.Null(options.HostIdentityProvider);
        Assert.Null(options.AdditionalHandlerAssemblies);
    }

    [Fact]
    public void Properties_CanCaptureScopedHostConfiguration()
    {
        var commandTarget = Substitute.For<ICommandTarget>();
        var options = new McpHostOptions
        {
            Lifetime = McpHostLifetimeStrategy.Scoped,
            CommandTarget = commandTarget,
            ResolveWorkspacePath = () => @"E:\repo",
        };

        Assert.Equal(McpHostLifetimeStrategy.Scoped, options.Lifetime);
        Assert.Same(commandTarget, options.CommandTarget);
        Assert.Equal(@"E:\repo", options.ResolveWorkspacePath?.Invoke());
    }

    [Fact]
    public void Properties_CanCaptureHostIdentityProviderConfiguration()
    {
        var identityProvider = Substitute.For<IHostIdentityProvider>();
        var options = new McpHostOptions
        {
            HostIdentityProvider = identityProvider,
        };

        Assert.Same(identityProvider, options.HostIdentityProvider);
    }
}
