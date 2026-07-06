using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class SettingsCommandHandlerTests : IDisposable
{
    private readonly RecordingVoiceChatSettingsStore _store = new();

    public SettingsCommandHandlerTests()
    {
        VoiceChatSettingsService.Instance.ConfigureStore(_store);
    }

    [Fact]
    public async Task SaveSettingsHandler_PersistsCommandSettingsPayload()
    {
        var settings = new VoiceChatSettings
        {
            Language = "fr-FR",
            AutoContinueEnabled = false,
            WakePhrase = "Okay Tracker",
            WakeSensitivity = "high",
            AutoListenOnWake = false,
            PicovoiceAccessKey = "access-key"
        };

        var handler = new SaveSettingsHandler(VoiceChatSettingsService.Instance);
        var result = await handler.HandleAsync(new SaveSettingsCommand(settings), new CallContext());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(settings);
        _store.SavedSettings.Should().NotBeNull();
        _store.SavedSettings.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void SettingsViewModel_SaveSettings_DispatchesVoiceSettingsPayload()
    {
        var source = ReadWorkspaceFile("src/McpServerManager.Core/ViewModels/SettingsViewModel.cs");

        source.Should().Contain("new SaveSettingsCommand(new VoiceChatSettings");
        source.Should().NotContain("_voiceChatSettingsService.Save(new VoiceChatSettings");
    }

    public void Dispose()
    {
        VoiceChatSettingsService.Instance.ConfigureStore(new FileVoiceChatSettingsStore());
    }

    private static string ReadWorkspaceFile(string relativePath)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath)),
            Path.GetFullPath(relativePath),
            Path.Combine("F:/GitHub/McpServerManager", relativePath.Replace('/', Path.DirectorySeparatorChar))
        };

        var path = candidates.FirstOrDefault(File.Exists);
        Assert.True(path is not null, $"Source file not found. Tried: {string.Join("; ", candidates)}");
        return File.ReadAllText(path);
    }

    private sealed class RecordingVoiceChatSettingsStore : IVoiceChatSettingsStore
    {
        public bool SupportsWakeWordSettings => true;

        public VoiceChatSettings? SavedSettings { get; private set; }

        public VoiceChatSettings Load() => SavedSettings ?? new VoiceChatSettings();

        public void Save(VoiceChatSettings settings) => SavedSettings = settings;
    }
}
