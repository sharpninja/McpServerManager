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
}
