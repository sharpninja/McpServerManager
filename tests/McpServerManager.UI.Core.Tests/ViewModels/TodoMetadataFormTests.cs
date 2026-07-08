using System.Linq;
using System.Reflection;
using McpServer.Cqrs;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

/// <summary>
/// UI-TODO-001 / FR-TODO-METAFORM-001 + TR-TODO-EDITOR-BODYONLY-001: verifies the Todo metadata
/// form on <see cref="TodoListHostViewModel"/> - front-matter fields, the depends-on buildable list,
/// body-only editor text, and the compose round-trip. Uses the UI.Core DI harness with a mock API.
/// </summary>
public sealed class TodoMetadataFormTests
{
    private static TodoListHostViewModel BuildHost()
    {
        var auth = Substitute.For<IAuthorizationPolicyService>();
        auth.CanExecuteAction(Arg.Any<string>()).Returns(true);
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Substitute.For<ITodoApiClient>());
        services.AddSingleton(Substitute.For<IWorkspaceApiClient>());
        services.AddSingleton(auth);
        services.AddCqrs(typeof(TodoMetadataFormTests).Assembly);
        services.AddUiCore();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<TodoListHostViewModel>();
    }

    private static void InvokeProtected(TodoListHostViewModel vm, string name, params object?[] args)
        => typeof(TodoListHostViewModel)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(vm, args);

    /// <summary>NewTodo splits the blank template: form fields populated, editor body has no front matter.</summary>
    [Fact]
    public void NewTodo_PopulatesFormAndBodyOnlyEditor()
    {
        var vm = BuildHost();
        InvokeProtected(vm, "NewTodo");

        Assert.Equal("NEW-TODO", vm.EditorFmId);
        Assert.Equal("mvp-app", vm.EditorFmSection);
        Assert.Equal("low", vm.EditorFmPriority);
        Assert.DoesNotContain("---", vm.EditorText);
        Assert.DoesNotContain("id:", vm.EditorText);
        Assert.Contains("Implementation Tasks", vm.EditorText);
    }

    /// <summary>SyncMetadataForm reads front-matter fields including depends-on.</summary>
    [Fact]
    public void SyncMetadataForm_ReadsDependsOn()
    {
        var vm = BuildHost();
        const string doc = "---\nid: MCP-X-001\nsection: backend\npriority: high\ndepends-on:\n  - MCP-X-000\n---\n\n# T\n";
        InvokeProtected(vm, "SyncMetadataForm", doc);

        Assert.Equal("MCP-X-001", vm.EditorFmId);
        Assert.Equal("backend", vm.EditorFmSection);
        Assert.Equal("high", vm.EditorFmPriority);
        Assert.Equal(new[] { "MCP-X-000" }, vm.EditorFmDependsOn.ToArray());
    }

    /// <summary>The depends-on buildable list add/remove commands mutate the collection.</summary>
    [Fact]
    public void DependsOnCommands_AddAndRemove()
    {
        var vm = BuildHost();
        vm.NewDependsOnEntry = "MCP-Y-001";
        vm.AddDependsOnCommand.Execute(null);
        Assert.Contains("MCP-Y-001", vm.EditorFmDependsOn);
        Assert.Equal("", vm.NewDependsOnEntry);

        // Duplicate is ignored.
        vm.NewDependsOnEntry = "MCP-Y-001";
        vm.AddDependsOnCommand.Execute(null);
        Assert.Single(vm.EditorFmDependsOn);

        vm.RemoveDependsOnCommand.Execute("MCP-Y-001");
        Assert.Empty(vm.EditorFmDependsOn);
    }

    /// <summary>ComposeEditorDocument rebuilds a full document from the form + body that round-trips.</summary>
    [Fact]
    public void ComposeEditorDocument_RoundTrips()
    {
        var vm = BuildHost();
        vm.EditorFmId = "MCP-Z-001";
        vm.EditorFmSection = "ui";
        vm.EditorFmPriority = "medium";
        vm.EditorFmEstimate = "1h";
        vm.EditorFmPhase = "construction";
        vm.NewDependsOnEntry = "MCP-Z-000";
        vm.AddDependsOnCommand.Execute(null);

        var doc = vm.ComposeEditorDocument("# Title\n\nBody here.");
        var fm = TodoMarkdown.ParseFrontMatter(doc);

        Assert.Equal("MCP-Z-001", fm.Id);
        Assert.Equal("ui", fm.Section);
        Assert.Equal("medium", fm.Priority);
        Assert.Equal("1h", fm.Estimate);
        Assert.Equal("construction", fm.Phase);
        Assert.Equal(new[] { "MCP-Z-000" }, fm.DependsOn.ToArray());
        Assert.Contains("Body here.", TodoMarkdown.ExtractBody(doc));
    }
}
