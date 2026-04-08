using System;
using Avalonia;
using Avalonia.Controls;
using McpServerManager.UI.Core.Services;
using McpServerManager.Android.Services;

namespace McpServerManager.Android.Views;

/// <summary>
/// Switches between PhoneMainView and TabletMainView based on actual Android display width.
/// Listens for configuration changes (fold/unfold, rotation) via DeviceFormFactor.
/// </summary>
public partial class AdaptiveMainView : UserControl
{
    private bool? _isTabletLayout;
    private Panel? _hostPanel;

    public AdaptiveMainView()
    {
        InitializeComponent();
        _hostPanel = this.FindControl<Panel>("HostPanel");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DeviceFormFactor.DisplayChanged += OnDisplayChanged;
        EvaluateLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DeviceFormFactor.DisplayChanged -= OnDisplayChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDisplayChanged()
    {
        UiDispatcherHost.Post(EvaluateLayout);
    }

    private void EvaluateLayout()
    {
        double widthDp = DeviceFormFactor.GetCurrentWidthDp();
        if (widthDp <= 0) return;

        bool shouldBeTablet = DeviceFormFactor.IsTablet();

        if (_isTabletLayout.HasValue && shouldBeTablet == _isTabletLayout.Value)
            return;

        _isTabletLayout = shouldBeTablet;
        var dc = DataContext;

        Control newView = shouldBeTablet
            ? new TabletMainView()
            : new PhoneMainView();

        newView.DataContext = dc;

        if (_hostPanel != null)
        {
            _hostPanel.Children.Clear();
            _hostPanel.Children.Add(newView);
        }
    }
}
