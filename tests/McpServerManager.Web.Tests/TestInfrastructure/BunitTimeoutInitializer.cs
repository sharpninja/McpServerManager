using System.Runtime.CompilerServices;
using Bunit;

namespace McpServerManager.Web.Tests.TestInfrastructure;

/// <summary>
/// Raises bUnit's default WaitFor* ceiling for this assembly. Pages load their view models in
/// OnInitializedAsync/OnParametersSetAsync, and with xUnit running test classes in parallel
/// the 1 second bUnit default can elapse before the post-load render on a busy machine. The
/// assertions are unchanged; WaitFor* still returns as soon as they pass.
/// </summary>
internal static class BunitTimeoutInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(30);
}