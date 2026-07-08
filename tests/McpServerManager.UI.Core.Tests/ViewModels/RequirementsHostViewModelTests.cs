using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

/// <summary>
/// PLAN-REQSDESKTOP-001 / FR-REQS-DESKTOP-001 + FR-REQS-PUSH-001 + FR-REQS-CROSSLINK-001: verifies the
/// requirements host VM loads all tabs, pushes the wiki, and drives the crosslink navigation stack.
/// </summary>
public sealed class RequirementsHostViewModelTests
{
    private static (RequirementsHostViewModel vm, IRequirementsApiClient client, IRequirementsWikiPublisher publisher) Build()
    {
        var auth = Substitute.For<IAuthorizationPolicyService>();
        auth.CanExecuteAction(Arg.Any<string>()).Returns(true);
        var client = Substitute.For<IRequirementsApiClient>();
        client.ListFunctionalRequirementsAsync(Arg.Any<CancellationToken>())
            .Returns(new FunctionalRequirementListResult(Array.Empty<FunctionalRequirementItem>()));
        client.ListTechnicalRequirementsAsync(Arg.Any<CancellationToken>())
            .Returns(new TechnicalRequirementListResult(Array.Empty<TechnicalRequirementItem>()));
        client.ListTestingRequirementsAsync(Arg.Any<CancellationToken>())
            .Returns(new TestingRequirementListResult(Array.Empty<TestingRequirementItem>()));
        client.ListMappingsAsync(Arg.Any<CancellationToken>())
            .Returns(new RequirementMappingListResult(Array.Empty<RequirementMappingItem>()));
        var publisher = Substitute.For<IRequirementsWikiPublisher>();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(auth);
        services.AddSingleton(client);
        services.AddSingleton(publisher);
        services.AddSingleton(Substitute.For<ITodoApiClient>());
        services.AddSingleton(Substitute.For<IWorkspaceApiClient>());
        services.AddCqrs(typeof(RequirementsHostViewModelTests).Assembly);
        services.AddUiCore();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<RequirementsHostViewModel>(), client, publisher);
    }

    /// <summary>LoadAll loads every requirement tab via the API client.</summary>
    [Fact]
    public async Task LoadAll_LoadsEveryTab()
    {
        var (vm, client, _) = Build();
        await vm.LoadAllCommand.ExecuteAsync(null);

        await client.Received(1).ListFunctionalRequirementsAsync(Arg.Any<CancellationToken>());
        await client.Received(1).ListTechnicalRequirementsAsync(Arg.Any<CancellationToken>());
        await client.Received(1).ListTestingRequirementsAsync(Arg.Any<CancellationToken>());
        await client.Received(1).ListMappingsAsync(Arg.Any<CancellationToken>());
        Assert.Equal("Requirements loaded.", vm.StatusMessage);
    }

    /// <summary>Push generates and publishes; the status reflects the published location.</summary>
    [Fact]
    public async Task PushToGitHub_PublishesAndReportsLocation()
    {
        var (vm, client, publisher) = Build();
        client.GenerateAsync(Arg.Any<GenerateRequirementsDocumentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedRequirementsDocument(new byte[] { 9 }, "text/markdown"));
        publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), WikiPushTarget.GitHub, Arg.Any<CancellationToken>())
            .Returns(new WikiPushResult(true, null, "https://gh/wiki"));

        await vm.PushToGitHubCommand.ExecuteAsync(null);

        Assert.Contains("https://gh/wiki", vm.StatusMessage);
        await publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), "text/markdown", WikiPushTarget.GitHub, Arg.Any<CancellationToken>());
    }

    /// <summary>Crosslink navigation pushes ids and toggles back/forward state.</summary>
    [Fact]
    public async Task Navigation_PushesAndTogglesBackForward()
    {
        var (vm, _, _) = Build();
        await vm.NavigateToRequirementCommand.ExecuteAsync("FR-1");
        Assert.Equal("FR-1", vm.CurrentRequirementId);
        Assert.False(vm.CanNavigateBack);

        await vm.NavigateToRequirementCommand.ExecuteAsync("FR-2");
        Assert.Equal("FR-2", vm.CurrentRequirementId);
        Assert.True(vm.CanNavigateBack);

        await vm.NavigateBackCommand.ExecuteAsync(null);
        Assert.Equal("FR-1", vm.CurrentRequirementId);
        Assert.True(vm.CanNavigateForward);
    }

    /// <summary>Import creates records and refreshes; the result counts are surfaced.</summary>
    [Fact]
    public async Task Import_CreatesAndReports()
    {
        var (vm, _, _) = Build();
        var request = new RequirementsImportRequest(
            new[] { new CreateFunctionalRequirementCommand("FR-9", "T", "B") },
            Array.Empty<CreateTechnicalRequirementCommand>(),
            Array.Empty<CreateTestingRequirementCommand>());

        var result = await vm.ImportAsync(request);

        Assert.NotNull(result);
        Assert.Equal(1, result!.FunctionalCreated);
    }
}
