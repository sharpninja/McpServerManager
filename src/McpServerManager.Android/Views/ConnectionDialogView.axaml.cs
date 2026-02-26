using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class ConnectionDialogView : UserControl
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("ConnectionDialogView");
    private ConnectionViewModel? _viewModel;
    private bool _loggedConnectReadyBounds;
    private bool _loggedOidcPanelReadyBounds;

    public ConnectionDialogView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _logger.LogInformation("UI_READY ConnectionDialogView attached. RootBounds={Bounds}", FormatBounds(Bounds));
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as ConnectionViewModel;
        _loggedConnectReadyBounds = false;
        _loggedOidcPanelReadyBounds = false;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _logger.LogInformation(
                "UI_READY ConnectionDialogView DataContext bound. Host={Host}, Port={Port}, IsConnecting={IsConnecting}",
                _viewModel.Host,
                _viewModel.Port,
                _viewModel.IsConnecting);
        }
        else
        {
            _logger.LogInformation("ConnectionDialogView DataContext cleared or not ConnectionViewModel");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        switch (e.PropertyName)
        {
            case nameof(ConnectionViewModel.IsConnecting):
                _logger.LogInformation("UI_STATE IsConnecting={IsConnecting}", _viewModel.IsConnecting);
                if (!_viewModel.IsConnecting)
                    _loggedConnectReadyBounds = false;
                break;
            case nameof(ConnectionViewModel.IsOidcSignInRequired):
                _logger.LogInformation("UI_STATE IsOidcSignInRequired={IsOidcSignInRequired}", _viewModel.IsOidcSignInRequired);
                if (_viewModel.IsOidcSignInRequired)
                    _loggedOidcPanelReadyBounds = false;
                break;
            case nameof(ConnectionViewModel.OidcCanOpenBrowser):
                _logger.LogInformation("UI_STATE OidcCanOpenBrowser={OidcCanOpenBrowser}", _viewModel.OidcCanOpenBrowser);
                break;
            case nameof(ConnectionViewModel.OidcStatusMessage):
                if (!string.IsNullOrWhiteSpace(_viewModel.OidcStatusMessage))
                    _logger.LogInformation("UI_STATE OidcStatusMessage={OidcStatusMessage}", _viewModel.OidcStatusMessage);
                break;
            case nameof(ConnectionViewModel.ErrorMessage):
                if (!string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
                    _logger.LogWarning("UI_STATE ErrorMessage={ErrorMessage}", _viewModel.ErrorMessage);
                break;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        TryLogConnectReadyBounds();
        TryLogOidcReadyBounds();
    }

    private void TryLogConnectReadyBounds()
    {
        if (_loggedConnectReadyBounds)
            return;

        var connectButton = this.FindControl<Button>("ConnectButton");
        var hostTextBox = this.FindControl<TextBox>("HostTextBox");
        var portTextBox = this.FindControl<TextBox>("PortTextBox");
        if (connectButton is null || hostTextBox is null || portTextBox is null)
            return;

        if (!HasRenderableBounds(connectButton) || !HasRenderableBounds(hostTextBox) || !HasRenderableBounds(portTextBox))
            return;

        _loggedConnectReadyBounds = true;
        _logger.LogInformation("UI_READY ConnectDialog input controls ready");
        LogControlBounds("HostTextBox", hostTextBox);
        LogControlBounds("PortTextBox", portTextBox);
        LogControlBounds("ConnectButton", connectButton);
    }

    private void TryLogOidcReadyBounds()
    {
        if (_loggedOidcPanelReadyBounds)
            return;

        if (_viewModel?.IsOidcSignInRequired != true)
            return;

        var oidcPanel = this.FindControl<Border>("OidcPanel");
        if (oidcPanel is null || !oidcPanel.IsVisible || !HasRenderableBounds(oidcPanel))
            return;

        _loggedOidcPanelReadyBounds = true;
        _logger.LogInformation("UI_READY OIDC panel visible");
        LogControlBounds("OidcPanel", oidcPanel);

        var openSignInButton = this.FindControl<Button>("OpenSignInPageButton");
        if (openSignInButton is not null && openSignInButton.IsVisible && HasRenderableBounds(openSignInButton))
            LogControlBounds("OpenSignInPageButton", openSignInButton);
    }

    private void LogControlBounds(string name, Control control)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var topLevelBounds = topLevel?.Bounds;
        _logger.LogInformation(
            "UI_BOUNDS {ControlName} Visible={Visible} Enabled={Enabled} Bounds={Bounds} RootBounds={RootBounds}",
            name,
            control.IsVisible,
            control.IsEnabled,
            FormatBounds(control.Bounds),
            topLevelBounds is null ? "<null>" : FormatBounds(topLevelBounds.Value));
    }

    private static bool HasRenderableBounds(Control control)
        => control.Bounds.Width > 0 && control.Bounds.Height > 0;

    private static string FormatBounds(Rect rect)
        => $"[{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}]";
}
