using McpServerManager.VsExtension.McpTodo.Models;

namespace McpServerManager.VsExtension.McpTodo;

internal sealed class TodoListEntry
{
    public string PriorityGroup { get; set; } = "";
    public string DisplayLine { get; set; } = "";
    public TodoFlatItem? Item { get; set; }
}
