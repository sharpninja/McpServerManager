using McpServerManager.Core.Models.Json;

namespace McpServerManager.Core.Commands;

/// <summary>
/// Request detail viewing, selection, and search-row interaction.
/// </summary>
public interface IRequestDetailsTarget
{
    void ShowRequestDetails(SearchableEntry entry);
    void CloseRequestDetails();
    void NavigateToPreviousRequest();
    void NavigateToNextRequest();
    void SelectSearchEntry(SearchableEntry entry);
    void SearchRowTapped(SearchableEntry? entry);
    void SearchRowDoubleTapped(SearchableEntry? entry);
}
