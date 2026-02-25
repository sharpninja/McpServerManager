using Avalonia;
using Avalonia.Controls;

namespace McpServerManager.Android.Views;

public partial class RequestDetailsView : UserControl
{
    public static readonly StyledProperty<bool> ShowOriginalJsonProperty =
        AvaloniaProperty.Register<RequestDetailsView, bool>(nameof(ShowOriginalJson), true);

    public bool ShowOriginalJson
    {
        get => GetValue(ShowOriginalJsonProperty);
        set => SetValue(ShowOriginalJsonProperty, value);
    }

    public RequestDetailsView()
    {
        InitializeComponent();
        UpdateOriginalJsonVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowOriginalJsonProperty)
            UpdateOriginalJsonVisibility();
    }

    private void UpdateOriginalJsonVisibility()
    {
        if (OriginalJsonExpander != null)
            OriginalJsonExpander.IsVisible = ShowOriginalJson;
    }
}
