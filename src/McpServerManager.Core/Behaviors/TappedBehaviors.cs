using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace McpServerManager.Core.Behaviors;

/// <summary>Attached behavior: binds Tapped event to an ICommand, passing DataContext as parameter.</summary>
public static class TappedBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(TappedBehavior));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(TappedBehavior));

    static TappedBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static ICommand? GetCommand(Control c) => c.GetValue(CommandProperty);
    public static void SetCommand(Control c, ICommand? value) => c.SetValue(CommandProperty, value);
    public static object? GetCommandParameter(Control c) => c.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(Control c, object? value) => c.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(Control c, AvaloniaPropertyChangedEventArgs e)
    {
        c.RemoveHandler(InputElement.TappedEvent, OnTapped);
        if (e.NewValue is ICommand)
            c.AddHandler(InputElement.TappedEvent, OnTapped, RoutingStrategies.Bubble);
    }

    private static void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control c) return;
        var command = GetCommand(c);
        var parameter = GetCommandParameter(c) ?? c.DataContext;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}

/// <summary>Attached behavior: binds DoubleTapped event to an ICommand, passing DataContext as parameter.</summary>
public static class DoubleTappedBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(DoubleTappedBehavior));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(DoubleTappedBehavior));

    static DoubleTappedBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static ICommand? GetCommand(Control c) => c.GetValue(CommandProperty);
    public static void SetCommand(Control c, ICommand? value) => c.SetValue(CommandProperty, value);
    public static object? GetCommandParameter(Control c) => c.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(Control c, object? value) => c.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(Control c, AvaloniaPropertyChangedEventArgs e)
    {
        c.RemoveHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
        if (e.NewValue is ICommand)
            c.AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control c) return;
        var command = GetCommand(c);
        var parameter = GetCommandParameter(c) ?? c.DataContext;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
