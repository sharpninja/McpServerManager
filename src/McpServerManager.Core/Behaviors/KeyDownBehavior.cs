using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace McpServerManager.Core.Behaviors;

/// <summary>Attached behavior: executes a command when a specific key is pressed.</summary>
public static class KeyDownBehavior
{
    public static readonly AttachedProperty<Key?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, Key?>("Key", typeof(KeyDownBehavior));

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(KeyDownBehavior));

    static KeyDownBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static Key? GetKey(Control c) => c.GetValue(KeyProperty);
    public static void SetKey(Control c, Key? value) => c.SetValue(KeyProperty, value);
    public static ICommand? GetCommand(Control c) => c.GetValue(CommandProperty);
    public static void SetCommand(Control c, ICommand? value) => c.SetValue(CommandProperty, value);

    private static void OnCommandChanged(Control c, AvaloniaPropertyChangedEventArgs e)
    {
        c.KeyDown -= OnKeyDown;
        if (e.NewValue is ICommand)
            c.KeyDown += OnKeyDown;
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control c) return;
        var key = GetKey(c);
        if (key == null || e.Key != key.Value) return;
        // Shift+Enter is a newline; only fire on plain Enter
        if (e.Key == Key.Return && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;

        var command = GetCommand(c);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }
}
