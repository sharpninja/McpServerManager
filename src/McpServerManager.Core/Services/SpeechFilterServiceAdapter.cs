using System.Collections.Generic;

namespace McpServerManager.Core.Services;

/// <summary>
/// Adapts the app speech filter singleton to UI.Core settings abstractions.
/// </summary>
public sealed class SpeechFilterServiceAdapter : McpServer.UI.Core.Services.ISpeechFilterService
{
    private readonly SpeechFilterService _inner;

    public SpeechFilterServiceAdapter(SpeechFilterService? inner = null)
    {
        _inner = inner ?? SpeechFilterService.Instance;
    }

    public string FilterText
    {
        get => _inner.FilterText;
        set => _inner.FilterText = value;
    }

    public IReadOnlyList<string> GetFilterWords() => _inner.GetFilterWords();

    public List<string> ParseJsonPhraseList(string content) => SpeechFilterService.ParseJsonPhraseList(content);
}
