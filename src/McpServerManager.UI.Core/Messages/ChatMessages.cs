using System;
using System.Collections.Generic;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Query to load available prompt templates for chat.</summary>
public sealed record LoadChatPromptsQuery : IQuery<IReadOnlyList<PromptTemplate>>;

/// <summary>Query to load available chat models (e.g. from Ollama).</summary>
public sealed record LoadChatModelsQuery(string? PreferredModel) : IQuery<ChatLoadModelsResult>;

/// <summary>Query to expand a prompt template into text for the input box.</summary>
public sealed record PopulateChatPromptQuery(PromptTemplate? Prompt) : IQuery<string>;

/// <summary>Result of preparing a prompt for send (whether to auto-send).</summary>
public sealed record ChatPreparedPromptResult(bool ShouldSend, string PromptText);

/// <summary>Query equivalent for submit prompt decision.</summary>
public sealed record SubmitChatPromptQuery(PromptTemplate? Prompt) : IQuery<ChatPreparedPromptResult>;

/// <summary>Command to send a chat message (text + context + model).</summary>
public sealed record SendChatMessageCommand(string UserMessage, string ContextSummary, string? Model) : ICommand<ChatSendMessageResult>;

/// <summary>Command to open agent config file (side-effect via handler/service).</summary>
public sealed record OpenChatAgentConfigCommand : ICommand<ChatFileOpenResult>;

/// <summary>Command to open prompt templates file (side-effect via handler/service).</summary>
public sealed record OpenChatPromptTemplatesCommand : ICommand<ChatFileOpenResult>;
