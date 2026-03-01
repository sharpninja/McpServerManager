using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

/// <summary>ViewModel for the Settings tab. Exposes speech filter phrase configuration.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SpeechFilterService _speechFilter = SpeechFilterService.Instance;

    [ObservableProperty]
    private string _speechFilterWords = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel()
    {
        _speechFilterWords = _speechFilter.FilterText;
    }

    /// <summary>Saves the current filter phrase list to disk.</summary>
    [RelayCommand]
    private void SaveFilterWords()
    {
        _speechFilter.FilterText = SpeechFilterWords;
        StatusMessage = $"Saved {_speechFilter.GetFilterWords().Count} filter phrase(s).";
    }

    /// <summary>Reverts the editor text to the last saved values.</summary>
    [RelayCommand]
    private void RevertFilterWords()
    {
        SpeechFilterWords = _speechFilter.FilterText;
        StatusMessage = "Reverted to saved values.";
    }

    /// <summary>
    /// Imports phrases from file content. Supports plain text (one per line),
    /// JSON (string array), and YAML (dash-prefixed list).
    /// </summary>
    public void ImportFromFileContent(string content, string fileName)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusMessage = "Import file was empty.";
            return;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        List<string> phrases;

        try
        {
            phrases = ext switch
            {
                ".json" => ParseJsonPhrases(content),
                ".yaml" or ".yml" => ParseYamlPhrases(content),
                _ => ParsePlainTextPhrases(content)
            };
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            return;
        }

        if (phrases.Count == 0)
        {
            StatusMessage = "No phrases found in file.";
            return;
        }

        // Merge with existing (deduplicate, case-insensitive)
        var existing = _speechFilter.FilterText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var merged = existing
            .Concat(phrases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var newCount = merged.Count - existing.Count;
        SpeechFilterWords = string.Join(Environment.NewLine, merged);
        _speechFilter.FilterText = SpeechFilterWords;
        StatusMessage = $"Imported {newCount} new phrase(s) ({merged.Count} total). Saved.";
    }

    private static List<string> ParseJsonPhrases(string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        // Try object with a single array property
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                return prop.Value.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
            }
        }

        throw new InvalidOperationException("JSON must contain a string array.");
    }

    private static List<string> ParseYamlPhrases(string content)
    {
        // Simple YAML list parser: lines starting with "- "
        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("- ", StringComparison.Ordinal))
            .Select(l => l[2..].Trim().Trim('"', '\''))
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static List<string> ParsePlainTextPhrases(string content)
    {
        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }
}
