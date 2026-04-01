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
            typeof(McpServerManager.UI.Core.ViewModels.MainWindowViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.WorkspaceViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.WorkspaceViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.TodoListViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.TodoListHostViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ConnectionViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.ConnectionViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.SettingsViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.SettingsViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.LogViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.LogViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.VoiceConversationViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.VoiceConversationViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ChatWindowViewModel),
            typeof(McpServerManager.UI.Core.ViewModels.ChatWindowViewModel)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.ViewModelBase),
            typeof(McpServerManager.UI.Core.ViewModels.ViewModelBase)
        ];
        yield return
        [
            typeof(McpServerManager.Core.ViewModels.EditorTab),
            typeof(McpServerManager.UI.Core.ViewModels.EditorTab)
        ];
    }

    [Theory]
    [MemberData(nameof(HostWrapperBaseTypeExpectations))]
    public void HostWrapper_InheritsExpectedUiCoreBaseType(Type wrapperType, Type expectedBaseType)
    {
        wrapperType.BaseType.Should().Be(expectedBaseType);
    }
}
