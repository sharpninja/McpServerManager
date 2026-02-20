using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RequestTracker.Android.Views;

/// <summary>
/// Switches between PhoneMainView and TabletMainView based on available width.
/// Reacts to fold/unfold and rotation on foldable devices.
/// </summary>
public partial class AdaptiveMainView : UserControl
{
    private const double TabletWidthThreshold = 600;
    private bool _isTabletLayout;
    private Control? _currentView;
    private Panel? _hostPanel;

    public AdaptiveMainView()
    {
        InitializeComponent();
        _hostPanel = this.FindControl<Panel>("HostPanel");
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayout(e.NewSize.Width);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLayout(Bounds.Width);
    }

    private void UpdateLayout(double availableWidth)
    {
        bool shouldBeTablet = availableWidth >= TabletWidthThreshold;

        if (_currentView != null && shouldBeTablet == _isTabletLayout)
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

        _currentView = newView;
    }
}
