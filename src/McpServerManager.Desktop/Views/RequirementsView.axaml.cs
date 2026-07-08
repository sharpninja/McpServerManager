using Avalonia.Controls;
using Avalonia.Interactivity;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.Desktop.Views;

/// <summary>
/// PLAN-REQSDESKTOP-001: Requirements management tab. Binds to <see cref="RequirementsHostViewModel"/>.
/// </summary>
public partial class RequirementsView : UserControl
{
    /// <summary>Initializes the requirements view.</summary>
    public RequirementsView()
    {
        InitializeComponent();
    }

    /// <summary>Opens the requirement id typed into the crosslink box, pushing onto the nav stack.</summary>
    protected void OnOpenCrosslink(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RequirementsHostViewModel vm)
            return;

        var box = this.FindControl<TextBox>("CrosslinkIdBox");
        var id = box?.Text;
        if (!string.IsNullOrWhiteSpace(id))
            _ = vm.NavigateToRequirementCommand.ExecuteAsync(id.Trim());
    }
}
