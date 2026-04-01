using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using McpServerManager.Android.Services;
using McpServerManager.UI.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Android.Views;

public partial class PhoneSessionLogView : UserControl
{
    private enum PhoneSessionLogScreen
    {
        List,
        Detail
    }

    private PhoneSessionLogScreen _screen = PhoneSessionLogScreen.List;
    private MainWindowViewModel? _currentVm;

    public PhoneSessionLogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        UpdateScreenVisibility();
        RefreshDetailHeader();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AndroidBackNavigationService.BackRequested -= OnAndroidBackRequested;
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AndroidBackNavigationService.BackRequested -= OnAndroidBackRequested;
        AndroidBackNavigationService.BackRequested += OnAndroidBackRequested;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;

        _currentVm = DataContext as MainWindowViewModel;
        if (_currentVm != null)
            _currentVm.PropertyChanged += OnViewModelPropertyChanged;

        RefreshDetailHeader();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
            return;

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.SelectedNode):
                if (_screen == PhoneSessionLogScreen.List && vm.SelectedNode is { IsDirectory: false })
                    ShowScreen(PhoneSessionLogScreen.Detail);
                RefreshDetailHeader();
                break;

            case nameof(MainWindowViewModel.SelectedUnifiedTurn):
            case nameof(MainWindowViewModel.IsJsonVisible):
            case nameof(MainWindowViewModel.IsMarkdownVisible):
            case nameof(MainWindowViewModel.IsRequestDetailsVisible):
                if (_screen == PhoneSessionLogScreen.List && vm.IsRequestDetailsVisible)
                    ShowScreen(PhoneSessionLogScreen.Detail);
                RefreshDetailHeader();
                break;
        }
    }

    private void ShowScreen(PhoneSessionLogScreen screen)
    {
        _screen = screen;
        UpdateScreenVisibility();
        RefreshDetailHeader();
    }

    private void UpdateScreenVisibility()
    {
        if (ListScreen == null || DetailScreen == null)
            return;

        ListScreen.IsVisible = _screen == PhoneSessionLogScreen.List;
        DetailScreen.IsVisible = _screen == PhoneSessionLogScreen.Detail;
    }

    private void RefreshDetailHeader()
    {
        if (DetailTitleText == null || DetailSubtitleText == null)
            return;

        if (DataContext is not MainWindowViewModel vm)
        {
            DetailTitleText.Text = "Session Log";
            DetailSubtitleText.Text = "Select a session.";
            return;
        }

        if (vm.IsRequestDetailsVisible && vm.SelectedUnifiedTurn is { } request)
        {
            DetailTitleText.Text = string.IsNullOrWhiteSpace(request.RequestId)
                ? "Request Details"
                : request.RequestId;
            DetailSubtitleText.Text = JoinNonEmpty(
                string.IsNullOrWhiteSpace(request.Agent) ? null : request.Agent,
                string.IsNullOrWhiteSpace(request.Model) ? null : request.Model,
                string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
                fallback: "Request details");
            return;
        }

        if (vm.IsMarkdownVisible)
        {
            DetailTitleText.Text = string.IsNullOrWhiteSpace(vm.SelectedNode?.Name) ? "Preview" : vm.SelectedNode!.Name;
            DetailSubtitleText.Text = "Markdown / Source Preview";
            return;
        }

        if (vm.IsJsonVisible)
        {
            DetailTitleText.Text = string.IsNullOrWhiteSpace(vm.SelectedNode?.Name) ? "Session Requests" : vm.SelectedNode!.Name;
            DetailSubtitleText.Text = "Summary and Requests";
            return;
        }

        DetailTitleText.Text = string.IsNullOrWhiteSpace(vm.SelectedNode?.Name) ? "Session Viewer" : vm.SelectedNode!.Name;
        DetailSubtitleText.Text = "Select a session from the list.";
    }

    private static string JoinNonEmpty(string? part1, string? part2, string? part3, string fallback)
    {
        var a = string.IsNullOrWhiteSpace(part1) ? null : part1!.Trim();
        var b = string.IsNullOrWhiteSpace(part2) ? null : part2!.Trim();
        var c = string.IsNullOrWhiteSpace(part3) ? null : part3!.Trim();

        if (a == null && b == null && c == null)
            return fallback;

        if (a != null && b != null && c != null) return $"{a} | {b} | {c}";
        if (a != null && b != null) return $"{a} | {b}";
        if (a != null && c != null) return $"{a} | {c}";
        if (b != null && c != null) return $"{b} | {c}";
        return a ?? b ?? c ?? fallback;
    }

    private void OnAllJsonHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            vm.Nodes.Count > 0 &&
            vm.TreeItemTappedCommand.CanExecute(vm.Nodes[0]))
        {
            vm.TreeItemTappedCommand.Execute(vm.Nodes[0]);
        }

        ShowScreen(PhoneSessionLogScreen.Detail);
    }

    private void OnSessionLeafClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FileNode node } || node.IsDirectory)
            return;

        if (DataContext is MainWindowViewModel vm && vm.TreeItemTappedCommand.CanExecute(node))
            vm.TreeItemTappedCommand.Execute(node);

        ShowScreen(PhoneSessionLogScreen.Detail);
    }

    private void OnSessionDetailBackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsRequestDetailsVisible)
        {
            if (vm.CloseRequestDetailsCommand.CanExecute(null))
                vm.CloseRequestDetailsCommand.Execute(null);
            RefreshDetailHeader();
            return;
        }

        ShowScreen(PhoneSessionLogScreen.List);
    }

    private bool OnAndroidBackRequested()
    {
        if (!IsVisible)
            return false;

        if (_screen != PhoneSessionLogScreen.Detail)
            return false;

        if (DataContext is MainWindowViewModel vm && vm.IsRequestDetailsVisible)
        {
            if (vm.CloseRequestDetailsCommand.CanExecute(null))
                vm.CloseRequestDetailsCommand.Execute(null);
            return true;
        }

        ShowScreen(PhoneSessionLogScreen.List);
        return true;
    }
}

