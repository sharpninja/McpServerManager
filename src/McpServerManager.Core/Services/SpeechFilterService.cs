using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.Services;

/// <summary>
/// Manages a user-configurable list of filter words for text-to-speech output.
/// Lines containing any filter word (case-insensitive substring match) are excluded from TTS.
/// </summary>
public sealed class SpeechFilterService
{
    private static readonly ILogger Logger = AppLogService.Instance.CreateLogger("SpeechFilter");
    private const string FilterFileName = "speech-filter-words.txt";
    private static readonly Lazy<SpeechFilterService> LazyInstance = new(() => new SpeechFilterService());

    private readonly object _lock = new();
    private List<string> _filterWords = [];

    /// <summary>Singleton instance shared across the app.</summary>
    public static SpeechFilterService Instance => LazyInstance.Value;

    private SpeechFilterService()
    {
        Load();
    }

    /// <summary>Gets the path to the persisted filter words file.</summary>
    private static string GetFilePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServerManager");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, FilterFileName);
    }

    /// <summary>
    /// Gets or sets the raw filter text (one word/phrase per line).
    /// Setting this value persists the list to disk.
    /// </summary>
    public string FilterText
    {
        get
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _filterWords);
            }
        }
        set
        {
            lock (_lock)
            {
                _filterWords = ParseLines(value);
            }
            Save();
        }
    }

    /// <summary>
    /// Returns true if the given line should be excluded from TTS because it contains
    /// one or more filter words (case-insensitive substring match).
    /// </summary>
    public bool ShouldFilter(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        lock (_lock)
        {
            foreach (var word in _filterWords)
            {
                if (line.Contains(word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Returns a snapshot of the current filter words.</summary>
    public IReadOnlyList<string> GetFilterWords()
    {
        lock (_lock)
        {
            return _filterWords.ToList();
        }
    }

    private void Load()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                lock (_lock)
                {
                    _filterWords = ParseLines(text);
                }
                Logger.LogDebug("Loaded {Count} speech filter words", _filterWords.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load speech filter words");
        }
    }

    private void Save()
    {
        try
        {
            var path = GetFilePath();
            lock (_lock)
            {
                File.WriteAllText(path, string.Join(Environment.NewLine, _filterWords));
            }
            Logger.LogDebug("Saved {Count} speech filter words", _filterWords.Count);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save speech filter words");
        }
    }

    private static List<string> ParseLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }
}
