using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class PhoneSettingsView : UserControl
{
    public PhoneSettingsView()
    {
        InitializeComponent();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Filter Phrases",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text files") { Patterns = ["*.txt"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("YAML files") { Patterns = ["*.yaml", "*.yml"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            (DataContext as SettingsViewModel)?.ImportFromFileContent(content, file.Name);
        }
        catch (Exception ex)
        {
            if (DataContext is SettingsViewModel vm)
                vm.StatusMessage = $"Import error: {ex.Message}";
        }
    }
}
