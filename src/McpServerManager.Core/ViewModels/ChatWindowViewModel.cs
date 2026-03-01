using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using McpServerManager.Core.Utilities;

namespace McpServerManager.Core.ViewModels;

public partial class ChatWindowViewModel : ViewModelBase
{
    private readonly IMediator _mediator;
    private readonly Func<string> _getContext;
    private readonly Action<string?>? _onModelChanged;
    private readonly string? _initialModelFromConfig;
    private CancellationTokenSource? _sendCts;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private string _currentInput = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<string> _availableModels = new();

    [ObservableProperty]
    private string? _selectedModel;

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> _promptTemplates = new();

    public ChatWindowViewModel(
        IMediator mediator,
        Func<string> getContext,
        string? initialModelFromConfig = null,
        Action<string?>? onModelChanged = null)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _getContext = getContext ?? (() => "");
        _initialModelFromConfig = initialModelFromConfig;
        _onModelChanged = onModelChanged;
    }

    /// <summary>Parameterless constructor for design-time only.</summary>
    public ChatWindowViewModel() : this(new Mediator(), () => "", null, null) { }

    [RelayCommand]
    private async Task OpenAgentConfig()
    {
        _ = await _mediator.SendAsync<ChatOpenAgentConfigCommand, ChatFileOpenResult>(new ChatOpenAgentConfigCommand()).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenPromptTemplates()
    {
        _ = await _mediator.SendAsync<ChatOpenPromptTemplatesCommand, ChatFileOpenResult>(new ChatOpenPromptTemplatesCommand()).ConfigureAwait(true);
    }

    /// <summary>Loads prompt templates from agent config. Call when the chat window is opened.</summary>
    public void LoadPrompts() => _ = LoadPromptsAsync();

    public async Task LoadPromptsAsync()
    {
        var prompts = await _mediator.SendAsync<ChatLoadPromptsCommand, System.Collections.Generic.IReadOnlyList<PromptTemplate>>(
            new ChatLoadPromptsCommand()).ConfigureAwait(true);

        DispatchToUi(() =>
        {
            PromptTemplates.Clear();
            foreach (var p in prompts)
                PromptTemplates.Add(p);
        });
    }

    [RelayCommand]
    private async Task PopulatePrompt(PromptTemplate? prompt)
    {
        var promptText = await _mediator.SendAsync<ChatPopulatePromptCommand, string>(new ChatPopulatePromptCommand(prompt)).ConfigureAwait(true);
        if (string.IsNullOrEmpty(promptText)) return;
        CurrentInput = promptText;
    }

    [RelayCommand]
    private async Task SubmitPromptAsync(PromptTemplate? prompt)
    {
        var prepared = await _mediator.SendAsync<ChatSubmitPromptCommand, ChatPreparedPromptResult>(new ChatSubmitPromptCommand(prompt)).ConfigureAwait(true);
        if (!prepared.ShouldSend) return;
        CurrentInput = prepared.PromptText;
        await SendCurrentInputAsync().ConfigureAwait(true);
    }

    partial void OnSelectedModelChanged(string? value)
    {
        _onModelChanged?.Invoke(value);
    }

    /// <summary>Loads available Ollama models and sets SelectedModel to the first or default. Call when the chat window is opened.</summary>
    public async Task LoadModelsAsync()
    {
        var result = await _mediator.QueryAsync<ChatLoadModelsQuery, ChatLoadModelsResult>(
            new ChatLoadModelsQuery(_initialModelFromConfig)).ConfigureAwait(true);

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

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync() => await SendCurrentInputAsync().ConfigureAwait(true);

    private async Task SendCurrentInputAsync()
    {
        var text = (CurrentInput ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return;

        CurrentInput = "";
        var userMsg = new ChatMessage { Role = "user", Text = text };
        Messages.Add(userMsg);
        var context = _getContext();
        IsLoading = true;
        SendCommand.NotifyCanExecuteChanged();

        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        var token = _sendCts.Token;

        var assistantMsg = new ChatMessage { Role = "assistant", Text = "" };
        DispatchToUi(() => Messages.Add(assistantMsg));

        IProgress<string>? progress = null;
        progress = new Progress<string>(content =>
        {
            DispatchToUi(() =>
            {
                if (!token.IsCancellationRequested && Messages.Contains(assistantMsg))
                    assistantMsg.Text = content ?? "";
            });
        });

        try
        {
            var model = SelectedModel;
            var request = new ChatSendRequest(text, context, model);
            var result = await _mediator.SendAsync<ChatSendMessageCommand, ChatSendMessageResult>(
                new ChatSendMessageCommand(request, progress),
                token).ConfigureAwait(true);

            if (token.IsCancellationRequested && !result.WasCancelled) return;

            DispatchToUi(() =>
            {
                if (token.IsCancellationRequested && !result.WasCancelled) return;
                assistantMsg.Text = result.WasCancelled
                    ? "[Cancelled]"
                    : (result.Success
                        ? TextTransformations.ConvertBareUrisToMarkdownLinks(result.ReplyText ?? "")
                        : "Error: " + (result.ErrorMessage ?? "Unknown error"));
                IsLoading = false;
                SendCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            DispatchToUi(() =>
            {
                assistantMsg.Text = "Error: " + ex.Message;
                IsLoading = false;
                SendCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanSend() => !IsLoading;

    private static void DispatchToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(() => action());
    }

    /// <summary>Cancels any in-flight send (e.g. when closing the window).</summary>
    public void CancelSend()
    {
        _sendCts?.Cancel();
    }

    /// <summary>Called when the user navigates; context is kept in sync and sent with the next user message (no chat message added to avoid spam).</summary>
    public void NotifyContextChanged(string fullContext)
    {
        // Context is retrieved via _getContext() when the user sends a message, so we do not add a visible message here.
    }
}
