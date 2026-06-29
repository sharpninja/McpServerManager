using System.Reflection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class RoutesHybridHostTests
{
    [Fact]
    public void RoutesExposesAdditionalAssembliesParameterForHybridHost()
    {
        var property = typeof(Routes).GetProperty(nameof(Routes.AdditionalAssemblies));

        Assert.NotNull(property);
        Assert.True(typeof(IEnumerable<Assembly>).IsAssignableFrom(property.PropertyType));
        Assert.Contains(property.GetCustomAttributes(), attribute => attribute is ParameterAttribute);
    }
}
