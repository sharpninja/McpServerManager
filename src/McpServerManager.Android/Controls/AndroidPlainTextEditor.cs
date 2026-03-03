using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;

namespace McpServerManager.Android.Controls;

public class AndroidPlainTextEditor : TextBox
{
    public AndroidPlainTextEditor()
    {
        AcceptsReturn = true;
        TextWrapping = TextWrapping.Wrap;
        SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        FontFamily = new FontFamily("avares://McpServerManager.Android/Assets/Fonts/FiraCode-Regular.ttf#Fira Code");
    }
}
