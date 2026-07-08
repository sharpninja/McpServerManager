using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace McpServerManager.Core.Behaviors;

/// <summary>
/// Attached behavior that binds a <see cref="FANavigationView.ItemInvoked"/> event
/// to an <see cref="ICommand"/>, passing the section key (Tag) as the command parameter.
/// Adapted from remote-agent for use in McpServerManager phone view.
/// (FluentAvalonia 3.x renamed NavigationView* to FANavigationView*.)
/// </summary>
public static class NavigationViewItemInvokedBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<FANavigationView, ICommand?>(
            "Command", typeof(NavigationViewItemInvokedBehavior));

    public static readonly AttachedProperty<string> SettingsKeyProperty =
        AvaloniaProperty.RegisterAttached<FANavigationView, string>(
            "SettingsKey", typeof(NavigationViewItemInvokedBehavior), "Settings");

    static NavigationViewItemInvokedBehavior()
    {
        CommandProperty.Changed.AddClassHandler<FANavigationView>(OnCommandChanged);
    }

    public static ICommand? GetCommand(FANavigationView nav) => nav.GetValue(CommandProperty);
    public static void SetCommand(FANavigationView nav, ICommand? value) => nav.SetValue(CommandProperty, value);

    public static string GetSettingsKey(FANavigationView nav) => nav.GetValue(SettingsKeyProperty);
    public static void SetSettingsKey(FANavigationView nav, string value) => nav.SetValue(SettingsKeyProperty, value);

    private static void OnCommandChanged(FANavigationView nav, AvaloniaPropertyChangedEventArgs e)
    {
        nav.ItemInvoked -= OnItemInvoked;

        if (e.NewValue is ICommand)
            nav.ItemInvoked += OnItemInvoked;
    }

    private static void OnItemInvoked(object? sender, FANavigationViewItemInvokedEventArgs e)
    {
        if (sender is not FANavigationView nav)
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
            sectionKey = e.InvokedItemContainer is FANavigationViewItem { Tag: string tag } ? tag : null;
        }

        if (sectionKey is not null && command.CanExecute(sectionKey))
            command.Execute(sectionKey);
    }
}
