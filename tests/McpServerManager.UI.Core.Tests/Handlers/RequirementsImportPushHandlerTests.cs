using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Handlers;

/// <summary>
/// PLAN-REQSDESKTOP-001 / FR-REQS-IMPORT-001 + FR-REQS-PUSH-001: verifies the requirements import and
/// wiki-push command handlers via the dispatcher, with mocked API client, publisher, and auth.
/// </summary>
public sealed class RequirementsImportPushHandlerTests
{
    private static (Dispatcher dispatcher, IRequirementsApiClient client, IRequirementsWikiPublisher publisher)
        Build(bool allow = true)
    {
        var auth = Substitute.For<IAuthorizationPolicyService>();
        auth.CanExecuteAction(Arg.Any<string>()).Returns(allow);
        var client = Substitute.For<IRequirementsApiClient>();
        var publisher = Substitute.For<IRequirementsWikiPublisher>();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(auth);
        services.AddSingleton(client);
        services.AddSingleton(publisher);
        services.AddSingleton(Substitute.For<ITodoApiClient>());
        services.AddSingleton(Substitute.For<IWorkspaceApiClient>());
        services.AddCqrs(typeof(RequirementsImportPushHandlerTests).Assembly);
        services.AddUiCore();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<Dispatcher>(), client, publisher);
    }

    /// <summary>Push generates the wiki then publishes it; a successful publish surfaces the location.</summary>
    [Fact]
    public async Task Push_GeneratesThenPublishes()
    {
        var (dispatcher, client, publisher) = Build();
        client.GenerateAsync(Arg.Any<GenerateRequirementsDocumentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedRequirementsDocument(new byte[] { 1, 2, 3 }, "text/markdown"));
        publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<WikiPushTarget>(), Arg.Any<CancellationToken>())
            .Returns(new WikiPushResult(true, null, "https://example/wiki"));

        var result = await dispatcher.SendAsync(new PushRequirementsWikiCommand(WikiPushTarget.GitHub));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        Assert.Equal("https://example/wiki", result.Value.Location);
        await client.Received(1).GenerateAsync(Arg.Is<GenerateRequirementsDocumentQuery>(q => q.Doc == "all"), Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => b.Length == 3), "text/markdown", WikiPushTarget.GitHub, Arg.Any<CancellationToken>());
    }

    /// <summary>Push is denied when the caller lacks the generate permission.</summary>
    [Fact]
    public async Task Push_DeniedWithoutPermission()
    {
        var (dispatcher, _, publisher) = Build(allow: false);
        var result = await dispatcher.SendAsync(new PushRequirementsWikiCommand(WikiPushTarget.Azure));
        Assert.False(result.IsSuccess);
        await publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<WikiPushTarget>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Import creates each parsed record and reports counts; a failing create is collected as an error.</summary>
    [Fact]
    public async Task Import_CreatesEach_AndCollectsErrors()
    {
        var (dispatcher, client, _) = Build();
        client.CreateTestingRequirementAsync(Arg.Any<CreateTestingRequirementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TestingRequirementItem>(new InvalidOperationException("dup")));

        var request = new RequirementsImportRequest(
            Functional: new[]
            {
                new CreateFunctionalRequirementCommand("FR-1", "T1", "B1"),
                new CreateFunctionalRequirementCommand("FR-2", "T2", "B2"),
            },
            Technical: new[] { new CreateTechnicalRequirementCommand("TR-1", "T", "B") },
            Testing: new[] { new CreateTestingRequirementCommand("TEST-1", "cond") });

        var result = await dispatcher.SendAsync(new ImportRequirementsCommand(request));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.FunctionalCreated);
        Assert.Equal(1, result.Value.TechnicalCreated);
        Assert.Equal(0, result.Value.TestingCreated);
        Assert.Single(result.Value.Errors);
        Assert.Contains("TEST-1", result.Value.Errors[0]);
        await client.Received(2).CreateFunctionalRequirementAsync(Arg.Any<CreateFunctionalRequirementCommand>(), Arg.Any<CancellationToken>());
    }
}
