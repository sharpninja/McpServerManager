using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

/// <summary>ViewModel for the Settings tab. Exposes speech filter word configuration.</summary>
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

    /// <summary>Saves the current filter word list to disk.</summary>
    [RelayCommand]
    private void SaveFilterWords()
    {
        _speechFilter.FilterText = SpeechFilterWords;
        StatusMessage = $"Saved {_speechFilter.GetFilterWords().Count} filter word(s).";
    }
}
