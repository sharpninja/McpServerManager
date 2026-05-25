namespace McpServerManager.Web.Components.Shared;

public sealed record DetailNavigationItem(
    string Key,
    string Href,
    string Label,
    string? Description = null);
