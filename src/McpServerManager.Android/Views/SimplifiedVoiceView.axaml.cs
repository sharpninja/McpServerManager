using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Android.App;
using AndroidOS = Android.OS;
using McpServerManager.Android.Services;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Android.Views;

public class ChatMessage : INotifyPropertyChanged
{
    private static readonly IBrush s_userBrush = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
    private static readonly IBrush s_assistantBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
    private static readonly IBrush s_systemBrush = new SolidColorBrush(Color.FromArgb(40, 200, 160, 0));

    private string _text = "";

    public string Role { get; init; } = "";

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public HorizontalAlignment HAlign =>
        IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Center;

    public IBrush BubbleBrush => IsUser ? s_userBrush : IsSystem ? s_systemBrush : s_assistantBrush;

    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsNotAssistant => !IsAssistant;

    private bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    private bool IsSystem => string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase);
}

public partial class SimplifiedVoiceView : UserControl
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("SimplifiedVoiceView");
    private static readonly Regex AnsiEscapePattern = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private readonly IAndroidSpeechRecognitionService _stt = new AndroidSpeechRecognitionService();
    private readonly IAndroidTextToSpeechService _tts = new AndroidTextToSpeechService();
    private readonly IAndroidAudioFocusService _audioFocus = new AndroidAudioFocusService();
    private readonly ObservableCollection<ChatMessage> _messages = new();

    private ScrollViewer? _chatScroller;
    private ToggleButton? _autoCheck;
    private Border? _inputPreviewBorder;
    private TextBlock? _inputPreviewText;

    private CancellationTokenSource? _loopCts;
    private bool _conversationActive;
    private bool _sessionReady;
    private bool _isDisposed;
    private bool _isPaused;
    private bool _isSpeaking;
    private bool _ttsStopped;
    private bool _clearRequested;
    private bool _sendRequested;
    private string? _manualText;
    private bool _foregroundServiceRunning;
    private TextBox? _textInputBox;
    private Button? _pauseButton;
    private Button? _stopButton;
    private Button? _chatToggleButton;

    public SimplifiedVoiceView()
    {
        InitializeComponent();
        _chatScroller = this.FindControl<ScrollViewer>("ChatScroller");
        _autoCheck = this.FindControl<ToggleButton>("AutoContinueToggle");
        _inputPreviewBorder = this.FindControl<Border>("InputPreviewBorder");
        _inputPreviewText = this.FindControl<TextBlock>("InputPreviewText");
        _pauseButton = this.FindControl<Button>("PauseResumeButton");
        _stopButton = this.FindControl<Button>("StopButton");
        _chatToggleButton = this.FindControl<Button>("ChatToggleButton");
        _textInputBox = this.FindControl<TextBox>("TextInputBox");

        var chatItems = this.FindControl<ItemsControl>("ChatItems");
        if (chatItems != null)
            chatItems.ItemsSource = _messages;

        if (_autoCheck != null)
            _autoCheck.IsCheckedChanged += OnAutoCheckedChanged;

        DetachedFromVisualTree += OnDetached;
    }

    private VoiceConversationViewModel? VM => DataContext as VoiceConversationViewModel;

    // ── Auto-continue toggle ───────────────────────────────────────────
    private async void OnAutoCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // Start the mic loop when auto-turn is enabled and the session is ready but idle.
        if (_autoCheck?.IsChecked != true || !_sessionReady || _conversationActive || _isDisposed)
            return;

        _loopCts?.Dispose();
        _loopCts = new CancellationTokenSource();
        _conversationActive = true;
        UpdateButtons();

        try
        {
            await RunConversationLoopAsync(_loopCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation loop failed");
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            _conversationActive = false;
            _isPaused = false;
            _isSpeaking = false;
            UpdateButtons();
            SetMicState(_sessionReady ? "ready" : "idle");
        }
    }

    // ── Start/End Chat toggle ──────────────────────────────────────────
    private async void OnChatToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_sessionReady)
        {
            // End the session
            _loopCts?.Cancel();
            _conversationActive = false;
            _tts.Stop();

            var vm = VM;
            if (vm != null && !string.IsNullOrWhiteSpace(vm.SessionId))
                await vm.EndSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

            _sessionReady = false;
            _isPaused = false;
            _isSpeaking = false;
            _ttsStopped = false;
            StopForegroundService();
            UpdateButtons();
            UpdateInputPreview(null);
            SetMicState("idle");
            SetStatus("Session ended.");
        }
        else
        {
            // Start a new session
            await StartSessionAsync().ConfigureAwait(true);
        }
    }

    private async Task StartSessionAsync()
    {
        var vm = VM;
        if (vm == null) return;

        if (_chatToggleButton != null)
            _chatToggleButton.IsEnabled = false;

        SetStatus("Creating session...");

        _messages.Add(new ChatMessage { Role = "system", Text = "Creating session..." });
        ScrollToBottom();

        await vm.CreateSessionCommand.ExecuteAsync(null).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(vm.SessionId))
        {
            var reason = string.IsNullOrWhiteSpace(vm.StatusText) ? "Failed to create session." : vm.StatusText;
            _logger.LogWarning("Voice session creation failed: {Reason}", reason);
            SetStatus(reason);
            _messages.Add(new ChatMessage { Role = "system", Text = reason });
            ScrollToBottom();
            if (_chatToggleButton != null)
                _chatToggleButton.IsEnabled = true;
            return;
        }

        _messages.Add(new ChatMessage { Role = "system", Text = $"Session {vm.SessionId}" });
        ScrollToBottom();

        // Seed session: tell Copilot to read workspace instructions
        await SeedSessionAsync(vm, CancellationToken.None).ConfigureAwait(true);

        _sessionReady = true;
        _conversationActive = true;
        _isPaused = false;
        _ttsStopped = false;
        _loopCts = new CancellationTokenSource();
        UpdateButtons();

        // Start foreground service to keep voice alive in background
        StartForegroundService("Voice session active. Listening...");

        // Announce readiness and start listening
        PlayChime();

        try
        {
            await RunConversationLoopAsync(_loopCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation loop failed");
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            _conversationActive = false;
            _isPaused = false;
            _isSpeaking = false;
            UpdateButtons();
            SetMicState(_sessionReady ? "ready" : "idle");
        }
    }

    // ── Main conversation loop ─────────────────────────────────────────
    private async Task RunConversationLoopAsync(CancellationToken ct)
    {
        var vm = VM;
        if (vm == null) return;

        // Signal that we're now listening
        SetStatus("Listening...");

        do
        {
            ct.ThrowIfCancellationRequested();

            // 1. Listen for speech with command detection
            var listenResult = await ListenForCommandAsync(ct).ConfigureAwait(true);

            // "end chat" with nothing accumulated → just end session
            if (listenResult.ShouldEndChat && string.IsNullOrWhiteSpace(listenResult.Transcript))
            {
                await EndSessionInternalAsync().ConfigureAwait(true);
                break;
            }

            if (string.IsNullOrWhiteSpace(listenResult.Transcript))
            {
                SetStatus("Listening...");
                continue;
            }

            var transcript = listenResult.Transcript!;

            // 2. Show user message in chat
            _messages.Add(new ChatMessage { Role = "user", Text = transcript });
            ScrollToBottom();

            // 3. Submit turn via streaming
            vm.TranscriptInput = transcript;
            SetMicState("thinking");

            var assistantBubble = new ChatMessage { Role = "assistant" };
            _messages.Add(assistantBubble);
            ScrollToBottom();

            var accumulated = new StringBuilder();
            var sentences = new List<string>();
            var sentenceBuffer = new StringBuilder();
            var spokenUpTo = 0;
            var isDone = false;
            _isSpeaking = true;
            _ttsStopped = false;
            UpdateButtons();

            await foreach (var evt in vm.SubmitTurnStreamingAsync(transcript, ct).ConfigureAwait(true))
            {
                ct.ThrowIfCancellationRequested();

                if (evt.Type == "chunk" && evt.Text is not null)
                {
                    accumulated.Append(AnsiEscapePattern.Replace(evt.Text, ""));
                    assistantBubble.Text = accumulated.ToString();
                    ScrollToBottom();

                    // Skip tool-progress lines for TTS (still displayed in chat)
                    if (IsToolProgressLine(evt.Text))
                        continue;

                    // Skip lines matching user-configured speech filter words
                    if (SpeechFilterService.Instance.ShouldFilter(evt.Text))
                        continue;

                    // Detect complete sentences and speak them as they arrive
                    sentenceBuffer.Append(evt.Text);
                    var bufText = sentenceBuffer.ToString();
                    var lastSentenceEnd = -1;
                    for (int i = 0; i < bufText.Length; i++)
                    {
                        if (bufText[i] is '.' or '!' or '?' &&
                            (i + 1 >= bufText.Length || bufText[i + 1] == ' ' || bufText[i + 1] == '\n'))
                        {
                            lastSentenceEnd = i;
                        }
                    }

                    if (lastSentenceEnd >= 0)
                    {
                        var completePart = bufText[..(lastSentenceEnd + 1)];
                        sentenceBuffer.Clear();
                        if (lastSentenceEnd + 1 < bufText.Length)
                            sentenceBuffer.Append(bufText[(lastSentenceEnd + 1)..].TrimStart());

                        foreach (var s in SplitIntoSentences(completePart))
                        {
                            if (!string.IsNullOrWhiteSpace(s))
                                sentences.Add(s);
                        }
                    }

                    // Speak any new sentences
                    while (spokenUpTo < sentences.Count && !_ttsStopped)
                    {
                        SetMicState("speaking");
                        try
                        {
                            await _tts.SpeakAsync(sentences[spokenUpTo], vm.Language, ct).ConfigureAwait(true);
                        }
                        catch (OperationCanceledException) when (_ttsStopped) { break; }
                        spokenUpTo++;
                    }
                }
                else if (evt.Type == "done")
                {
                    isDone = true;
                }
                else if (evt.Type == "error")
                {
                    _logger.LogWarning("Voice streaming error: {Message}", evt.Message);
                    assistantBubble.Text = $"Error: {evt.Message}";
                    ScrollToBottom();
                    break;
                }
            }

            // Convert bare URIs to markdown links in the final accumulated display text.
            if (isDone)
            {
                assistantBubble.Text = TextTransformations.ConvertBareUrisToMarkdownLinks(assistantBubble.Text ?? "");
                ScrollToBottom();
            }

            // Speak any remaining buffered text
            var remainder = sentenceBuffer.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(remainder) && !_ttsStopped)
            {
                SetMicState("speaking");
                try
                {
                    await _tts.SpeakAsync(remainder, vm.Language, ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException) { /* ok */ }
            }

            _isSpeaking = false;
            UpdateButtons();

            ct.ThrowIfCancellationRequested();
            if (listenResult.ShouldEndChat)
            {
                await EndSessionInternalAsync().ConfigureAwait(true);
                break;
            }

            // 7. Loop back if auto-continue is on
        } while (_autoCheck?.IsChecked == true && !ct.IsCancellationRequested);

        if (!ct.IsCancellationRequested)
            SetStatus("Tap mic to continue.");
    }

    // ── Seed session with workspace instructions ──────────────────────
    private async Task SeedSessionAsync(VoiceConversationViewModel vm, CancellationToken ct)
    {
        var workspacePath = vm.WorkspacePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _messages.Add(new ChatMessage { Role = "system", Text = "No workspace path — skipping seed." });
            ScrollToBottom();
            SetStatus("Session ready (no workspace seed).");
            return;
        }

        SetStatus("Seeding Copilot with workspace instructions...");

        var seedPrompt = "Read the file .github/copilot_instructions.md and follow those instructions for the remainder of this session.";

        _messages.Add(new ChatMessage { Role = "system", Text = seedPrompt });
        ScrollToBottom();

        var seedBubble = new ChatMessage { Role = "assistant" };
        _messages.Add(seedBubble);
        ScrollToBottom();

        var seedAccum = new StringBuilder();
        await foreach (var evt in vm.SubmitTurnStreamingAsync(seedPrompt, ct).ConfigureAwait(true))
        {
            if (evt.Type == "chunk" && evt.Text is not null)
            {
                seedAccum.Append(evt.Text);
                seedBubble.Text = seedAccum.ToString();
                ScrollToBottom();
            }
        }

        if (seedAccum.Length == 0)
            seedBubble.Text = vm.AssistantDisplayText ?? vm.StatusText ?? "(no response)";

        await _tts.SpeakAsync("Copilot ready", vm.Language, ct).ConfigureAwait(true);
        SetStatus("Copilot ready. Listening...");
    }

    // ── Listen loop with command detection ─────────────────────────────
    private async Task<ListenResult> ListenForCommandAsync(CancellationToken ct)
    {
        var accumulated = new StringBuilder();
        var emptyCount = 0;

        using var focusLease = _audioFocus.Acquire(AndroidVoiceAudioFocusUsage.SpeechRecognition);

        while (!ct.IsCancellationRequested)
        {
            SetMicState(_isPaused ? "paused" : "listening");

            string result;
            try
            {
                result = await _stt.RecognizeOnceAsync(VM?.Language, ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Speech recognition failed");
                SetStatus($"Mic error: {ex.Message} — retrying...");
                await Task.Delay(1000, ct).ConfigureAwait(true);
                continue;
            }

            // Check if clear was requested via the UI button
            if (_clearRequested)
            {
                _clearRequested = false;
                accumulated.Clear();
                UpdateInputPreview(null);
            }

            // Check if send was requested via the UI button or text input
            if (_sendRequested)
            {
                _sendRequested = false;
                var typed = _manualText;
                _manualText = null;
                if (!string.IsNullOrWhiteSpace(typed))
                {
                    UpdateInputPreview(null);
                    return new ListenResult(typed, false);
                }
                var final = accumulated.ToString().Trim();
                UpdateInputPreview(null);
                if (!string.IsNullOrWhiteSpace(final))
                    return new ListenResult(final, false);
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                emptyCount++;
                if (emptyCount >= 5)
                {
                    emptyCount = 0;
                    SetStatus("Still listening...");
                }
                continue;
            }

            emptyCount = 0;
            var (cleanText, command) = DetectCommand(result);

            // While paused, only respond to "resume chat"
            if (_isPaused)
            {
                if (command == VoiceCommand.ResumeChat)
                {
                    _isPaused = false;
                    UpdateButtons();
                    SetStatus("Resumed. Listening...");
                }
                continue;
            }

            switch (command)
            {
                case VoiceCommand.Send:
                case VoiceCommand.EndChat:
                    if (!string.IsNullOrEmpty(cleanText))
                    {
                        if (accumulated.Length > 0) accumulated.Append(' ');
                        accumulated.Append(cleanText);
                    }

                    var final = accumulated.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(final) && command == VoiceCommand.Send)
                    {
                        SetStatus("Nothing to send. Say your request first, then 'send now'.");
                        UpdateInputPreview(null);
                        continue;
                    }

                    UpdateInputPreview(null);
                    return new ListenResult(
                        string.IsNullOrWhiteSpace(final) ? null : final,
                        command == VoiceCommand.EndChat);

                case VoiceCommand.StartOver:
                case VoiceCommand.ClearChat:
                    accumulated.Clear();
                    UpdateInputPreview(null);
                    SetStatus("Cleared. Listening...");
                    continue;

                case VoiceCommand.PauseChat:
                    _isPaused = true;
                    UpdateButtons();
                    SetStatus("Paused. Say 'resume chat' or tap Resume.");
                    continue;

                case VoiceCommand.ResumeChat:
                    // Already not paused, treat as no-op
                    continue;

                case VoiceCommand.Continue:
                default:
                    if (accumulated.Length > 0) accumulated.Append(' ');
                    accumulated.Append(cleanText);
                    UpdateInputPreview(accumulated.ToString());
                    SetStatus("Heard so far. Say more, 'send now', or 'start over'.");
                    continue;
            }
        }

        ct.ThrowIfCancellationRequested();
        return new ListenResult(null, false);
    }

    // ── Voice command detection ────────────────────────────────────────

    private record ListenResult(string? Transcript, bool ShouldEndChat);

    private enum VoiceCommand { Continue, Send, StartOver, ClearChat, EndChat, PauseChat, ResumeChat }

    private static (string cleanText, VoiceCommand command) DetectCommand(string recognizedText)
    {
        var trimmed = recognizedText.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return ("", VoiceCommand.Continue);

        // Strip trailing punctuation for matching
        var normalized = trimmed.TrimEnd('.', '!', '?', ',', ';').Trim();
        var lower = normalized.ToLowerInvariant();

        // Standalone commands (entire utterance)
        if (lower is "start over" or "startover")
            return ("", VoiceCommand.StartOver);
        if (lower is "clear chat" or "clear")
            return ("", VoiceCommand.ClearChat);
        if (lower is "pause chat" or "pause")
            return ("", VoiceCommand.PauseChat);
        if (lower is "resume chat" or "resume")
            return ("", VoiceCommand.ResumeChat);

        // "end chat" at the end → submit accumulated + end session
        if (lower == "end chat")
            return ("", VoiceCommand.EndChat);
        if (lower.EndsWith(" end chat"))
        {
            var clean = normalized[..^" end chat".Length].TrimEnd('.', '!', '?', ',', ';').Trim();
            return (clean, VoiceCommand.EndChat);
        }

        // "send now" at the end → submit accumulated
        if (lower == "send now")
            return ("", VoiceCommand.Send);
        if (lower.EndsWith(" send now"))
        {
            var clean = normalized[..^" send now".Length].TrimEnd('.', '!', '?', ',', ';').Trim();
            return (clean, VoiceCommand.Send);
        }

        return (trimmed, VoiceCommand.Continue);
    }

    // ── UI helpers ─────────────────────────────────────────────────────

    private void SetMicState(string state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed) return;
            string? notificationText = null;
            switch (state)
            {
                case "idle":
                    SetStatus("Tap Start Chat to begin");
                    break;
                case "connecting":
                    SetStatus("Starting session...");
                    notificationText = "Starting session...";
                    break;
                case "ready":
                    SetStatus("Session ready.");
                    notificationText = "Session ready.";
                    break;
                case "listening":
                    SetStatus("Listening... say 'send now' when done");
                    notificationText = "Listening...";
                    break;
                case "paused":
                    SetStatus("Paused. Say 'resume chat' or tap Resume.");
                    notificationText = "Paused.";
                    break;
                case "thinking":
                    SetStatus("Thinking...");
                    notificationText = "Thinking...";
                    break;
                case "speaking":
                    SetStatus("Speaking...");
                    notificationText = "Speaking...";
                    break;
            }
            if (notificationText != null && _foregroundServiceRunning)
                UpdateForegroundServiceStatus(notificationText);
        });
    }

    private void SetStatus(string text)
    {
        if (VM != null) VM.StatusText = text;
    }

    private void UpdateInputPreview(string? text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_inputPreviewBorder != null)
                _inputPreviewBorder.IsVisible = !string.IsNullOrEmpty(text);
            if (_inputPreviewText != null)
                _inputPreviewText.Text = text ?? "";
        });
    }

    // ── Pause/Resume (contextual: listening vs speaking) ──────────────

    private void OnPauseResumeClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!_conversationActive) return;

        _isPaused = !_isPaused;
        UpdateButtons();

        if (_isSpeaking)
        {
            SetStatus(_isPaused ? "Playback paused. Tap Resume to continue." : "Resuming playback...");
        }
        else
        {
            if (!_isPaused) SetStatus("Resumed. Listening...");
            SetStatus(_isPaused ? "Paused. Say 'resume chat' or tap Resume." : "Resumed. Listening...");
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!_isSpeaking) return;

        _ttsStopped = true;
        _isPaused = false;
        _tts.Stop();
        UpdateButtons();
        SetStatus("Playback stopped.");
    }

    private void OnClearInputClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _clearRequested = true;
        UpdateInputPreview(null);
        SetStatus("Cleared. Listening...");
    }

    private void OnSendInputClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _sendRequested = true;
    }

    private void OnTextClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_textInputBox != null)
            _textInputBox.Text = string.Empty;
    }

    private void OnTextSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SubmitTypedText();
    }

    private void OnTextInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
        {
            e.Handled = true;
            SubmitTypedText();
        }
    }

    private void SubmitTypedText()
    {
        var text = _textInputBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        _manualText = text;
        _sendRequested = true;
        if (_textInputBox != null)
            _textInputBox.Text = string.Empty;
    }

    private void UpdateButtons()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_pauseButton != null)
            {
                _pauseButton.IsEnabled = _conversationActive;
                _pauseButton.Content = _isPaused
                    ? new Avalonia.Controls.PathIcon { Data = Avalonia.Media.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z"), Width = 24, Height = 24 }
                    : new Avalonia.Controls.PathIcon { Data = Avalonia.Media.Geometry.Parse("M14,19H18V5H14M6,19H10V5H6V19Z"), Width = 24, Height = 24 };
            }
            if (_stopButton != null)
                _stopButton.IsEnabled = _isSpeaking;
            if (_chatToggleButton != null)
            {
                _chatToggleButton.IsEnabled = true;
                _chatToggleButton.Content = _sessionReady ? "End" : "Start";
            }
        });
    }

    // ── Streaming TTS (sentence-by-sentence with pause/stop) ──────────

    private async Task SpeakResponseStreamingAsync(
        string displayText, string speakText, string language, CancellationToken ct)
    {
        _isSpeaking = true;
        _ttsStopped = false;
        _isPaused = false;
        SetMicState("speaking");
        UpdateButtons();

        var displaySentences = SplitIntoSentences(displayText);
        var speakSentences = SplitIntoSentences(speakText);

        // Create the chat bubble, initially showing the first sentence
        var bubble = new ChatMessage { Role = "assistant" };
        _messages.Add(bubble);

        var displayed = new StringBuilder();
        using var focusLease = _audioFocus.Acquire(AndroidVoiceAudioFocusUsage.TextToSpeechPlayback);

        var count = Math.Max(displaySentences.Length, speakSentences.Length);
        for (int i = 0; i < count && !_ttsStopped; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Wait while paused
            while (_isPaused && !_ttsStopped && !ct.IsCancellationRequested)
                await Task.Delay(200, ct).ConfigureAwait(true);

            if (_ttsStopped) break;

            // Stream display sentence into bubble
            if (i < displaySentences.Length)
            {
                if (displayed.Length > 0) displayed.Append(' ');
                displayed.Append(displaySentences[i]);
                bubble.Text = displayed.ToString();
                ScrollToBottom();
            }

            // Speak the corresponding sentence
            if (i < speakSentences.Length)
            {
                try
                {
                    await _tts.SpeakAsync(speakSentences[i], language, ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException) when (_ttsStopped)
                {
                    break;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // TTS interrupted (e.g. Stop() during speaking), break
                    break;
                }
            }
        }

        // Show any remaining display text that wasn't streamed
        if (displayed.Length < displayText.Length)
        {
            bubble.Text = displayText;
            ScrollToBottom();
        }

        _isSpeaking = false;
        UpdateButtons();
    }

    private static string[] SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var sentences = new List<string>();
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '!' or '?' &&
                (i + 1 >= text.Length || text[i + 1] == ' '))
            {
                var sentence = text[start..(i + 1)].Trim();
                if (!string.IsNullOrEmpty(sentence))
                    sentences.Add(sentence);
                start = i + 1;
                if (start < text.Length && text[start] == ' ')
                    start++;
            }
        }

        if (start < text.Length)
        {
            var remainder = text[start..].Trim();
            if (!string.IsNullOrEmpty(remainder))
                sentences.Add(remainder);
        }

        return sentences.Count > 0 ? sentences.ToArray() : [text.Trim()];
    }

    private async Task EndSessionInternalAsync()
    {
        _tts.Stop();
        _isPaused = false;
        _conversationActive = false;

        var vm = VM;
        if (vm != null && !string.IsNullOrWhiteSpace(vm.SessionId))
            await vm.EndSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        _sessionReady = false;
        _isSpeaking = false;
        _ttsStopped = false;
        StopForegroundService();
        UpdateButtons();
        UpdateInputPreview(null);
        SetMicState("idle");
        SetStatus("Chat ended.");
    }

    /// <summary>
    /// Returns true if the line is a Copilot CLI tool-execution marker that
    /// should be displayed but not spoken (spinners, commands, fold summaries).
    /// </summary>
    private static bool IsToolProgressLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return false;

        char first = trimmed[0];

        // Spinner / tool-start markers: ● ◐ ◑ ◒ ◓
        if (first is '\u25CF' or '\u25D0' or '\u25D1' or '\u25D2' or '\u25D3')
            return true;

        // Result fold: └
        if (first == '\u2514')
            return true;

        // Error/success markers: ✗ ✘ ✓
        if (first is '\u2717' or '\u2718' or '\u2713')
            return true;

        // Shell command lines (indented with $)
        if (line.Length >= 3 && line[0] == ' ' && line[1] == ' ' && line[2] == '$')
            return true;

        // Indented command output (2+ leading spaces)
        if (line.Length >= 2 && line[0] == ' ' && line[1] == ' ')
            return true;

        return false;
    }

    private static void PlayChime()
    {
        try
        {
            var toneGen = new global::Android.Media.ToneGenerator(
                global::Android.Media.Stream.Notification, 80);
            toneGen.StartTone(global::Android.Media.Tone.PropAck, 200);
            // Let the tone finish before cleanup
            Task.Delay(250).ContinueWith(_ =>
            {
                try { toneGen.Release(); toneGen.Dispose(); }
                catch (Exception ex) { _logger.LogDebug(ex, "ToneGenerator cleanup failed"); }
            });
        }
        catch (Exception ex) { _logger.LogDebug(ex, "PlayChime failed"); }
    }

    private void ScrollToBottom()
    {
        // Double-post: first at Background lets markdown re-layout, second scrolls after
        Dispatcher.UIThread.Post(() =>
            Dispatcher.UIThread.Post(() => _chatScroller?.ScrollToEnd(), DispatcherPriority.Background),
            DispatcherPriority.Background);
    }

    // ── Foreground service helpers ───────────────────────────────────────

    private void StartForegroundService(string statusText)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            using var intent = VoiceSessionForegroundService.CreateStartIntent(context, statusText);
            if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
            _foregroundServiceRunning = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start voice foreground service");
        }
    }

    private void StopForegroundService()
    {
        if (!_foregroundServiceRunning) return;
        try
        {
            var context = global::Android.App.Application.Context;
            using var intent = VoiceSessionForegroundService.CreateStopIntent(context);
            context.StartService(intent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop voice foreground service");
        }
        finally
        {
            _foregroundServiceRunning = false;
        }
    }

    private void UpdateForegroundServiceStatus(string statusText)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            using var intent = VoiceSessionForegroundService.CreateUpdateIntent(context, statusText);
            context.StartService(intent);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Failed to update foreground service status"); }
    }

    // ── Cleanup ────────────────────────────────────────────────────────

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDisposed) return;
        _isDisposed = true;
        DetachedFromVisualTree -= OnDetached;
        if (_autoCheck != null)
            _autoCheck.IsCheckedChanged -= OnAutoCheckedChanged;
        _loopCts?.Cancel();
        _tts.Stop();
        _tts.Dispose();
        _stt.Dispose();
        _audioFocus.Dispose();
        StopForegroundService();
    }
}
