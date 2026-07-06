using System.Text.RegularExpressions;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Architecture;

public sealed class CqrsRefactorSourceGateTests
{
    [Fact]
    public void CoreCommands_DoNotDependOnUiOrPlatformEventServices()
    {
        var root = FindWorkspaceRoot();
        var commandsRoot = Path.Combine(root, "src", "McpServerManager.Core", "Commands");
        var text = string.Join(Environment.NewLine, Directory.EnumerateFiles(commandsRoot, "*.cs").Select(File.ReadAllText));

        Assert.DoesNotContain("IVoiceChatSettingsService", text);
        Assert.DoesNotContain("IAgentEventStreamService", text);
    }

    [Fact]
    public void AgentEventListenerCommand_IsDefinedOnceInUiCoreCommands()
    {
        var root = FindWorkspaceRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories);
        var definitions = sourceFiles
            .Select(path => (Path: path, Count: Regex.Matches(File.ReadAllText(path), @"record\s+StartAgentEventListenerCommand\b").Count))
            .Where(item => item.Count > 0)
            .ToArray();

        Assert.Equal(1, definitions.Sum(item => item.Count));
        Assert.Contains(Path.Combine("McpServerManager.UI.Core", "Commands"), definitions.Single().Path);
    }

    [Fact]
    public void MainWindowViewModel_DoesNotOwnAgentEventLoop()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/MainWindowViewModel.cs");

        Assert.Contains("new StartAgentEventListenerCommand(restart)", source);
        Assert.Contains("new StopAgentEventListenerCommand()", source);
        Assert.DoesNotContain("RunAgentEventListenerLoopAsync", source);
        Assert.DoesNotContain("StreamEventsAsync(", source);
        Assert.DoesNotContain("McpAgentEventStreamService _agentEventStreamService", source);
        Assert.DoesNotContain("CancellationTokenSource? _agentEventListenerCts", source);
        Assert.DoesNotContain("IsActionableAgentEvent", source);
        Assert.DoesNotContain("BuildActionableAgentEventMessage", source);
    }

    [Fact]
    public void InitializeAfterWindowShown_StartsAgentEventListenerThroughCommandDispatch()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/MainWindowViewModel.cs");

        Assert.Contains("StartAgentEventListener();", source);
        Assert.Contains("_dispatcher.SendAsync(new StartAgentEventListenerCommand(restart))", source);
    }

    [Fact]
    public void Logout_StopsAgentEventListenerThroughCommandDispatch()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/MainWindowViewModel.cs");

        Assert.Contains("StopAgentEventListener();", source);
        Assert.Contains("_dispatcher.SendAsync(new StopAgentEventListenerCommand())", source);
    }

    [Fact]
    public void AndroidVoiceViews_DoNotDirectlyConstructAndroidVoiceServices()
    {
        var viewSources = new[]
        {
            ReadWorkspaceFile("src/McpServerManager.Android/Views/VoiceConversationView.axaml.cs"),
            ReadWorkspaceFile("src/McpServerManager/Views/SimplifiedVoiceView.axaml.cs")
        };

        foreach (var source in viewSources)
        {
            Assert.DoesNotContain("new AndroidSpeechRecognitionService", source);
            Assert.DoesNotContain("new AndroidTextToSpeechService", source);
            Assert.DoesNotContain("new AndroidAudioFocusService", source);
            Assert.DoesNotContain("new AndroidWakeWordService", source);
        }
    }

    [Fact]
    public void AndroidVoiceViews_DoNotOwnAndroidVoicePlatformWorkflow()
    {
        var conversationSource = ReadWorkspaceFile("src/McpServerManager.Android/Views/VoiceConversationView.axaml.cs");
        var simplifiedSource = ReadWorkspaceFile("src/McpServerManager/Views/SimplifiedVoiceView.axaml.cs");

        Assert.DoesNotContain("IAndroidSpeechRecognitionService", conversationSource);
        Assert.DoesNotContain("IAndroidTextToSpeechService", conversationSource);
        Assert.DoesNotContain("IAndroidAudioFocusService", conversationSource);
        Assert.DoesNotContain("IAndroidWakeWordService", conversationSource);
        Assert.DoesNotContain("VoiceChatSettingsService _voiceChatSettingsService", conversationSource);
        Assert.DoesNotContain("AndroidVoiceAudioFocusUsage", conversationSource);
        Assert.DoesNotContain("AndroidWakeWordSettings", conversationSource);

        Assert.DoesNotContain("IAndroidSpeechRecognitionService", simplifiedSource);
        Assert.DoesNotContain("IAndroidTextToSpeechService", simplifiedSource);
        Assert.DoesNotContain("IAndroidAudioFocusService", simplifiedSource);
        Assert.DoesNotContain("global::Android.App.Application.Context", simplifiedSource);
        Assert.DoesNotContain("VoiceSessionForegroundService.Create", simplifiedSource);
        Assert.DoesNotContain("AndroidVoiceAudioFocusUsage", simplifiedSource);
    }

    [Fact]
    public void AndroidAppServiceFactory_ConfiguresBothAndroidVoiceViewFactories()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.Android/Services/AndroidAppServiceFactory.cs");

        Assert.Contains("McpServerManager.Android.Views.VoiceConversationView.ControllerFactory", source);
        Assert.Contains("McpServerManager.Android.Views.SimplifiedVoiceView.PlatformControllerFactory", source);
        Assert.DoesNotContain("McpServerManager.Android.Views.VoiceConversationView.SpeechFactory", source);
        Assert.DoesNotContain("McpServerManager.Android.Views.SimplifiedVoiceView.SpeechFactory", source);
    }

    [Fact]
    public void CodeBehindFiles_DoNotDeclarePrivateMethods()
    {
        var root = FindWorkspaceRoot();
        var matches = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.axaml.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"(?m)^\s*private\s+.*\(")
                .Select(match => $"{Path.GetRelativePath(root, path)}:{LineNumber(File.ReadAllText(path), match.Index)}:{match.Value.Trim()}"))
            .ToArray();

        Assert.True(matches.Length == 0, "Private methods remain in .axaml.cs files:" + Environment.NewLine + string.Join(Environment.NewLine, matches));
    }

    [Fact]
    public void C3WorkspaceAgentConfigurationViewModels_DoNotDeclarePrivateLogicHelpers()
    {
        var root = FindWorkspaceRoot();
        var viewModelsRoot = Path.Combine(root, "src", "McpServerManager.UI.Core", "ViewModels");
        var targetFiles = Directory.EnumerateFiles(viewModelsRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Contains("Workspace", StringComparison.Ordinal)
                       || name.Contains("Agent", StringComparison.Ordinal)
                       || name.Contains("Configuration", StringComparison.Ordinal);
            });

        var pattern = new Regex(@"(?m)^\s*private\s+.*\b(Create|Update|Apply|Load|Execute|Replace|Refresh)\w*\s*\(");
        var matches = targetFiles
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return pattern.Matches(text)
                    .Select(match => $"{Path.GetRelativePath(root, path)}:{LineNumber(text, match.Index)}:{match.Value.Trim()}");
            })
            .ToArray();

        Assert.True(matches.Length == 0, "Private C3 VM logic helpers remain:" + Environment.NewLine + string.Join(Environment.NewLine, matches));
    }

    [Fact]
    public void MainWindowAndAgentPoolViewModels_DoNotUseTaskRun()
    {
        var sources = new[]
        {
            ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/MainWindowViewModel.cs"),
            ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/AgentPoolViewModel.cs")
        };

        foreach (var source in sources)
        {
            Assert.DoesNotContain("Task.Run", source);
        }
    }

    [Fact]
    public void MainWindowViewModel_DoesNotDeclarePrivateAppLogicHelpers()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.UI.Core/ViewModels/MainWindowViewModel.cs");
        var matches = Regex.Matches(source, @"(?m)^\s*private\s+.*\b(Create|Update|Apply|Load|Execute|Replace|Refresh|Dispatch|Run|Open|Render|Preview|Index|Filter|Start|Stop|Connect|Watch)\w*\s*\(")
            .Select(match => $"{LineNumber(source, match.Index)}:{match.Value.Trim()}")
            .ToArray();

        Assert.True(matches.Length == 0, "Private MainWindow app-logic helpers remain:" + Environment.NewLine + string.Join(Environment.NewLine, matches));
    }

    [Fact]
    public void BannedMockingLibrary_IsNotUsedInSourceOrTests()
    {
        var root = FindWorkspaceRoot();
        var bannedToken = string.Concat("M", "oq");
        var searchedRoots = new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "tests"),
            Path.Combine(root, "lib", "McpServer", "src"),
            Path.Combine(root, "lib", "McpServer", "tests")
        };

        var matches = searchedRoots
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return Regex.Matches(text, $@"\b{Regex.Escape(bannedToken)}\b")
                    .Select(match => $"{Path.GetRelativePath(root, path)}:{LineNumber(text, match.Index)}");
            })
            .ToArray();

        Assert.True(matches.Length == 0, $"{bannedToken} references remain:" + Environment.NewLine + string.Join(Environment.NewLine, matches));
    }

    private static string ReadWorkspaceFile(string relativePath)
    {
        var path = Path.Combine(FindWorkspaceRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"source file {relativePath} should exist");
        return File.ReadAllText(path);
    }

    private static int LineNumber(string text, int index)
        => text[..Math.Min(index, text.Length)].Count(c => c == '\n') + 1;

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "McpServerManager.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        return "F:/GitHub/McpServerManager";
    }
}
