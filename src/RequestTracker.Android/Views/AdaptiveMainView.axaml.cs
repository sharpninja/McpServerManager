using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace RequestTracker.Android.Views;

/// <summary>
/// Switches between PhoneMainView and TabletMainView based on available width.
/// Reacts to fold/unfold and rotation on foldable devices.
/// </summary>
public partial class AdaptiveMainView : UserControl
{
    private const double TabletWidthThreshold = 600;
    private bool? _isTabletLayout;
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
        EvaluateLayout();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Also listen to Bounds property changes for Android config changes
        this.GetObservable(BoundsProperty).Subscribe(new BoundsObserver(this));
        EvaluateLayout();
    }

    private sealed class BoundsObserver : IObserver<Rect>
    {
        private readonly AdaptiveMainView _owner;
        public BoundsObserver(AdaptiveMainView owner) => _owner = owner;
        public void OnNext(Rect value) => _owner.EvaluateLayout();
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EvaluateLayout(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void EvaluateLayout(double? widthOverride = null)
    {
        double width = widthOverride ?? Bounds.Width;
        if (width <= 0) return;

        bool shouldBeTablet = width >= TabletWidthThreshold;

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

        _currentView = newView;
    }
}
