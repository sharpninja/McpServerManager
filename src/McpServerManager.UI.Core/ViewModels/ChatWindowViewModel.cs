using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using ChatPreparedPromptMessageResult = McpServerManager.UI.Core.Messages.ChatPreparedPromptResult;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// ViewModel for chat assistant window interactions.
/// </summary>
public partial class ChatWindowViewModel : ViewModelBase
{
    private static readonly Regex BareUriPattern = new(
        @"(?<!\(|\[)https?://[^\s)\]`""<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IDispatcher _dispatcher;
    private readonly IUiDispatcherService _uiDispatcher;
    private readonly Func<string> _getContext;
    private readonly Action<string?>? _onModelChanged;
    private readonly string? _initialModelFromConfig;
    private CancellationTokenSource? _sendCts;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = [];

    [ObservableProperty]
    private string _currentInput = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<string> _availableModels = [];

    [ObservableProperty]
    private string? _selectedModel;

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> _promptTemplates = [];

    /// <summary>
    /// Creates a chat window ViewModel.
    /// </summary>
    public ChatWindowViewModel(
        IDispatcher dispatcher,
        Func<string>? getContext = null,
        string? initialModelFromConfig = null,
        Action<string?>? onModelChanged = null,
        IUiDispatcherService? uiDispatcher = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _uiDispatcher = uiDispatcher ?? new ImmediateUiDispatcherService();
        _getContext = getContext ?? (() => string.Empty);
        _initialModelFromConfig = initialModelFromConfig;
        _onModelChanged = onModelChanged;
    }

    /// <summary>
    /// Opens the agent config file in default editor.
    /// </summary>
    public async Task<ChatFileOpenResult> OpenAgentConfigAsync(CancellationToken cancellationToken = default)
    {
        var res = await _dispatcher.SendAsync(new OpenChatAgentConfigCommand(), cancellationToken).ConfigureAwait(true);
        return res.IsSuccess ? res.Value! : new ChatFileOpenResult(false, null, res.Error);
    }

    /// <summary>
    /// Opens the prompt templates file in default editor.
    /// </summary>
    public async Task<ChatFileOpenResult> OpenPromptTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var res = await _dispatcher.SendAsync(new OpenChatPromptTemplatesCommand(), cancellationToken).ConfigureAwait(true);
        return res.IsSuccess ? res.Value! : new ChatFileOpenResult(false, null, res.Error);
    }

    /// <summary>
    /// Loads prompt templates from backing service.
    /// </summary>
    public void LoadPrompts() => _ = LoadPromptsAsync();

    /// <summary>
    /// Loads prompt templates from backing service.
    /// </summary>
    public async Task LoadPromptsAsync(CancellationToken cancellationToken = default)
    {
        var res = await _dispatcher.QueryAsync(new LoadChatPromptsQuery(), cancellationToken).ConfigureAwait(true);
        var prompts = res.IsSuccess && res.Value is not null ? res.Value : Array.Empty<PromptTemplate>();
        DispatchToUi(() =>
        {
            PromptTemplates.Clear();
            foreach (var prompt in prompts)
                PromptTemplates.Add(prompt);
        });
    }

    partial void OnSelectedModelChanged(string? value)
    {
        _onModelChanged?.Invoke(value);
    }

    /// <summary>
    /// Loads available models and applies initial preferred model selection.
    /// </summary>
    public async Task LoadModelsAsync(CancellationToken cancellationToken = default)
    {
        var res = await _dispatcher.QueryAsync(new LoadChatModelsQuery(_initialModelFromConfig), cancellationToken).ConfigureAwait(true);
        var result = res.IsSuccess ? res.Value! : new ChatLoadModelsResult(false, Array.Empty<string>(), null);
        DispatchToUi(() =>
        {
            AvailableModels.Clear();
            if (!result.IsReachable)
            {
                AvailableModels.Add("(Ollama not reachable)");
                SelectedModel = null;
                return;
            }

            if (result.Models.Count == 0)
            {
                AvailableModels.Add("(No models - start Ollama)");
                SelectedModel = null;
                return;
            }

            foreach (var model in result.Models)
                AvailableModels.Add(model);

            SelectedModel = result.SelectedModel;
        });
    }

    /// <summary>
    /// Populates the input box from selected prompt template.
    /// </summary>
    protected async Task PopulatePrompt(PromptTemplate? prompt)
    {
        var res = await _dispatcher.QueryAsync(new PopulateChatPromptQuery(prompt), default).ConfigureAwait(true);
        var promptText = res.IsSuccess ? res.Value : string.Empty;
        if (string.IsNullOrEmpty(promptText))
            return;

        CurrentInput = promptText;
    }

    /// <summary>
    /// Submits selected prompt template as a chat message when appropriate.
    /// </summary>
    protected async Task SubmitPromptAsync(PromptTemplate? prompt)
    {
        var res = await _dispatcher.QueryAsync(new SubmitChatPromptQuery(prompt), default).ConfigureAwait(true);
        var prepared = res.IsSuccess ? res.Value! : new ChatPreparedPromptMessageResult(false, string.Empty);
        if (!prepared.ShouldSend)
            return;

        CurrentInput = prepared.PromptText;
        await SendAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Sends the current input to the assistant.
    /// </summary>
    protected async Task SendAsync()
    {
        _sendCts?.Cancel();
        using var sendCts = new CancellationTokenSource();
        _sendCts = sendCts;
        var cmd = new SendChatMessageCommand(CurrentInput ?? string.Empty, _getContext(), SelectedModel);
        try
        {
            var res = await _dispatcher.SendAsync(cmd, sendCts.Token).ConfigureAwait(true);
            ApplySendResult(res);
        }
        finally
        {
            if (ReferenceEquals(_sendCts, sendCts))
                _sendCts = null;
        }
    }

    private void ApplySendResult(Result<ChatSendMessageResult> res)
    {
        CurrentInput = string.Empty;
        IsLoading = false;
        NotifySendCanExecuteChanged();
        // UI projection from result 
        if (res.IsSuccess && res.Value != null && !string.IsNullOrEmpty(res.Value.ReplyText))
        {
            // placeholder; in full, would append/update messages from val
        }
    }

    /// <summary>
    /// Returns true when the send action can execute.
    /// </summary>
    protected bool CanSend()
        => !IsLoading;

    /// <summary>
    /// Cancels any in-flight send request.
    /// </summary>
    public void CancelSend()
    {
        _sendCts?.Cancel();
    }

    /// <summary>
    /// Called when surrounding context changes.
    /// </summary>
    public void NotifyContextChanged(string fullContext)
    {
        // Context is read from _getContext when sending.
    }



    private static string ConvertBareUrisToMarkdownLinks(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return BareUriPattern.Replace(text, match => $"[{match.Value}]({match.Value})");
    }

    private void DispatchToUi(Action action)
        => _uiDispatcher.Post(action);

    /// <summary>
    /// Notifies command infrastructure that send availability changed.
    /// </summary>
    protected virtual void NotifySendCanExecuteChanged()
    {
    }
}
