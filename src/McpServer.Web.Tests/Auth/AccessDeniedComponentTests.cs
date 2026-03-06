using Bunit;
using McpServer.Web.Pages.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServer.Web.Tests.Auth;

public class AccessDeniedComponentTests
{
    [Fact]
    public void AccessDenied_RendersCorrectly()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        
        var cut = ctx.Render<AccessDenied>();
        
        cut.WaitForAssertion(() => Assert.Contains("Access Denied", cut.Markup, StringComparison.OrdinalIgnoreCase));
        cut.WaitForAssertion(() => Assert.Contains("You do not have permission to view this page", cut.Markup, StringComparison.OrdinalIgnoreCase));
    }
}
