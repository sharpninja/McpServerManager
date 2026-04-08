using McpServerManager.Web.Tests.TestInfrastructure;
using Xunit;

namespace McpServerManager.Web.Tests.Auth;

/// <summary>
/// Integration tests for the <c>/access-denied</c> Blazor page.
/// Verifies that the page is accessible without authentication and renders correctly.
/// </summary>
public sealed class AccessDeniedPageTests
{
    [Fact]
    public async Task GetAccessDenied_WithoutAuth_Returns200()
    {
        using var factory = WebTestFactory.Create();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/access-denied");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
