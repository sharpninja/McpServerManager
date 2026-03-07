using FluentAssertions;
using Xunit;

namespace McpServerManager.Core.Tests.Integration;

public sealed class UiCoreHostWrapperInheritanceTests
{
    public static IEnumerable<object[]> HostWrapperBaseTypeExpectations()
    {
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.MainWindowViewModel),
            typeof(McpServer.UI.Core.ViewModels.MainWindowViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.WorkspaceViewModel),
            typeof(McpServer.UI.Core.ViewModels.WorkspaceViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.TodoListViewModel),
            typeof(McpServer.UI.Core.ViewModels.TodoListHostViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ConnectionViewModel),
            typeof(McpServer.UI.Core.ViewModels.ConnectionViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.SettingsViewModel),
            typeof(McpServer.UI.Core.ViewModels.SettingsViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.LogViewModel),
            typeof(McpServer.UI.Core.ViewModels.LogViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.VoiceConversationViewModel),
            typeof(McpServer.UI.Core.ViewModels.VoiceConversationViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ChatWindowViewModel),
            typeof(McpServer.UI.Core.ViewModels.ChatWindowViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ViewModelBase),
            typeof(McpServer.UI.Core.ViewModels.ViewModelBase)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.EditorTab),
            typeof(McpServer.UI.Core.ViewModels.EditorTab)
        ];
    }

    [Theory]
    [MemberData(nameof(HostWrapperBaseTypeExpectations))]
    public void HostWrapper_InheritsExpectedUiCoreBaseType(Type wrapperType, Type expectedBaseType)
    {
        wrapperType.BaseType.Should().Be(expectedBaseType);
    }
}
