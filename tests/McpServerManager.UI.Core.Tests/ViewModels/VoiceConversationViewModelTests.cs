using System.Text.Json;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

public sealed class VoiceConversationViewModelTests
{
    [Fact]
    public async Task BuildTranscriptTextForExportAsync_RefreshesAndIncludesAllEntries()
    {
        var service = new TestVoiceConversationService(CreateTranscriptEntries());
        var vm = new VoiceConversationViewModel(service)
        {
            SessionId = "session-1"
        };

        var text = await vm.BuildTranscriptTextForExportAsync();

        Assert.Contains("[2026-05-21T20:00:00Z] user/input (turn-1)", text);
        Assert.Contains("hello", text);
        Assert.Contains("[2026-05-21T20:00:01Z] assistant/output (turn-1)", text);
        Assert.Contains("reply", text);
        Assert.Equal(2, vm.TranscriptItems.Count);
    }

    [Fact]
    public async Task BuildTranscriptJsonLinesForExportAsync_WritesOneJsonObjectPerEntry()
    {
        var service = new TestVoiceConversationService(CreateTranscriptEntries());
        var vm = new VoiceConversationViewModel(service)
        {
            SessionId = "session-1"
        };

        var jsonl = await vm.BuildTranscriptJsonLinesForExportAsync();

        var lines = jsonl.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        using var first = JsonDocument.Parse(lines[0]);
        var root = first.RootElement;
        Assert.Equal("session-1", root.GetProperty("sessionId").GetString());
        Assert.Equal("2026-05-21T20:00:00Z", root.GetProperty("timestampUtc").GetString());
        Assert.Equal("turn-1", root.GetProperty("turnId").GetString());
        Assert.Equal("user", root.GetProperty("role").GetString());
        Assert.Equal("input", root.GetProperty("category").GetString());
        Assert.Equal("hello", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task CopyTranscriptAsync_CopiesSharedPlainTextToClipboard()
    {
        var clipboard = new RecordingClipboardService();
        var service = new TestVoiceConversationService(CreateTranscriptEntries());
        var vm = new VoiceConversationViewModel(service, clipboardService: clipboard)
        {
            SessionId = "session-1"
        };

        await vm.CopyTranscriptAsync();

        Assert.NotNull(clipboard.Text);
        Assert.Contains("hello", clipboard.Text);
        Assert.Contains("reply", clipboard.Text);
        Assert.Equal("Copied 2 transcript item(s).", vm.StatusText);
    }

    [Fact]
    public async Task SubmitTurnStreamingAsync_WhenStreamHasNoChunks_LoadsAssistantTranscriptFallback()
    {
        var service = new TestVoiceConversationService(
            CreateTranscriptEntries(),
            [
                new McpVoiceTurnStreamEvent
                {
                    Type = "done",
                    TurnId = "turn-1",
                    Status = "completed",
                    LatencyMs = 44
                }
            ]);
        var vm = new VoiceConversationViewModel(service)
        {
            SessionId = "session-1"
        };

        var events = new List<McpVoiceTurnStreamEvent>();
        await foreach (var evt in vm.SubmitTurnStreamingAsync("hello"))
            events.Add(evt);

        Assert.Single(events);
        Assert.Equal("done", events[0].Type);
        Assert.Equal("reply", vm.AssistantDisplayText);
        Assert.Equal("reply", vm.AssistantSpeakText);
        Assert.Equal("Voice turn completed (44 ms)", vm.StatusText);
    }

    private static IReadOnlyList<McpVoiceTranscriptEntry> CreateTranscriptEntries() =>
    [
        new()
        {
            TimestampUtc = "2026-05-21T20:00:00Z",
            TurnId = "turn-1",
            Role = "user",
            Category = "input",
            Text = "hello"
        },
        new()
        {
            TimestampUtc = "2026-05-21T20:00:01Z",
            TurnId = "turn-1",
            Role = "assistant",
            Category = "output",
            Text = "reply"
        }
    ];

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public Task SetTextAsync(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class TestVoiceConversationService : IVoiceConversationService
    {
        private readonly IReadOnlyList<McpVoiceTranscriptEntry> _entries;
        private readonly IReadOnlyList<McpVoiceTurnStreamEvent> _streamEvents;

        public TestVoiceConversationService(
            IReadOnlyList<McpVoiceTranscriptEntry> entries,
            IReadOnlyList<McpVoiceTurnStreamEvent>? streamEvents = null)
        {
            _entries = entries;
            _streamEvents = streamEvents ?? [];
        }

        public Func<string?>? ResolveWorkspacePath { get; set; }

        public string? WorkspacePath { get; set; }

        public Task<McpVoiceSessionCreateResponse> CreateSessionAsync(
            McpVoiceSessionCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<McpVoiceTurnResponse> SubmitTurnAsync(
            string sessionId,
            McpVoiceTurnRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<McpVoiceTurnStreamEvent> SubmitTurnStreamingAsync(
            string sessionId,
            McpVoiceTurnRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var evt in _streamEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return evt;
            }
        }

        public Task<McpVoiceInterruptResponse> InterruptAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> SendEscapeAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<McpVoiceSessionStatus> GetStatusAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<McpVoiceTranscriptResponse> GetTranscriptAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new McpVoiceTranscriptResponse
            {
                SessionId = sessionId,
                Items = _entries
            });

        public Task<McpVoiceSessionStatus?> FindExistingSessionAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
