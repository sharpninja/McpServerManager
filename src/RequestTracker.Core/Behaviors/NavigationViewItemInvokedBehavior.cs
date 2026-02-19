using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace RequestTracker.Core.Behaviors;

/// <summary>
/// Attached behavior that binds a <see cref="NavigationView.ItemInvoked"/> event
/// to an <see cref="ICommand"/>, passing the section key (Tag) as the command parameter.
/// Adapted from remote-agent for use in RequestTracker phone view.
/// </summary>
public static class NavigationViewItemInvokedBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<NavigationView, ICommand?>(
            "Command", typeof(NavigationViewItemInvokedBehavior));

    public static readonly AttachedProperty<string> SettingsKeyProperty =
        AvaloniaProperty.RegisterAttached<NavigationView, string>(
            "SettingsKey", typeof(NavigationViewItemInvokedBehavior), "Settings");

    static NavigationViewItemInvokedBehavior()
    {
        CommandProperty.Changed.AddClassHandler<NavigationView>(OnCommandChanged);
    }

    public static ICommand? GetCommand(NavigationView nav) => nav.GetValue(CommandProperty);
    public static void SetCommand(NavigationView nav, ICommand? value) => nav.SetValue(CommandProperty, value);

    public static string GetSettingsKey(NavigationView nav) => nav.GetValue(SettingsKeyProperty);
    public static void SetSettingsKey(NavigationView nav, string value) => nav.SetValue(SettingsKeyProperty, value);

    private static void OnCommandChanged(NavigationView nav, AvaloniaPropertyChangedEventArgs e)
    {
        nav.ItemInvoked -= OnItemInvoked;

        if (e.NewValue is ICommand)
            nav.ItemInvoked += OnItemInvoked;
    }

    private static void OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (sender is not NavigationView nav)
            return;

        var command = GetCommand(nav);
        if (command is null)
            return;

        string? sectionKey;
        if (e.IsSettingsInvoked)
        {
            sectionKey = GetSettingsKey(nav);
        }
        else
        {
            sectionKey = e.InvokedItemContainer is NavigationViewItem { Tag: string tag } ? tag : null;
        }

        if (sectionKey is not null && command.CanExecute(sectionKey))
            command.Execute(sectionKey);
    }
}
