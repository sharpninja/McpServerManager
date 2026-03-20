using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using UiDispatcherHost = McpServer.UI.Core.Services.UiDispatcherHost;

namespace McpServerManager.Android.Views;

public partial class AnimatedStatusBar : UserControl
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("AnimatedStatusBar");
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AnimatedStatusBar, bool>(nameof(IsBusy));

    public static readonly StyledProperty<IBrush?> IdleBrushProperty =
        AvaloniaProperty.Register<AnimatedStatusBar, IBrush?>(nameof(IdleBrush));

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public IBrush? IdleBrush
    {
        get => GetValue(IdleBrushProperty);
        set => SetValue(IdleBrushProperty, value);
    }

    private Timer? _timer;
    private double _hue;
    private volatile bool _animating;

    public AnimatedStatusBar()
    {
        InitializeComponent();
        Padding = new Thickness(8, 4);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            _logger.LogDebug("IsBusy changed to {IsBusy}", IsBusy);
            if (IsBusy)
                StartAnimation();
            else
                StopAnimation();
        }
        else if (change.Property == IdleBrushProperty && !IsBusy)
        {
            Background = IdleBrush;
        }
    }

    private void StartAnimation()
    {
        _animating = true;
        _hue = 0;
        _timer?.Dispose();
        _timer = new Timer(_ =>
        {
            if (!_animating) return;
            _hue = (_hue + 3) % 360;
            UiDispatcherHost.Post(() =>
            {
                if (!_animating) return;
                Background = new SolidColorBrush(HslToColor(_hue, 0.7, 0.55));
            });
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
    }

    private void StopAnimation()
    {
        _animating = false;
        _timer?.Dispose();
        _timer = null;

        // Reset to theme brush
        IBrush? brush = null;
        if (Application.Current != null &&
            Application.Current.TryGetResource("StatusBarBrush", Application.Current.ActualThemeVariant, out var res) &&
            res is IBrush themeBrush)
        {
            brush = themeBrush;
        }
        else if (IdleBrush != null)
        {
            brush = IdleBrush;
        }

        if (brush != null)
            Background = brush;
        else
            ClearValue(BackgroundProperty);

        _logger.LogInformation("StopAnimation: background reset to {BrushType}", brush?.GetType().Name ?? "cleared");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        _timer?.Dispose();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private static Color HslToColor(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = l - c / 2;
        double r, g, b;
        if (h < 60)       { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else               { r = c; g = 0; b = x; }
        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
