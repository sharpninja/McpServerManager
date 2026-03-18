using McpServer.UI.Core.Models.Json;

namespace McpServer.UI.Core.Commands;

/// <summary>
/// Request detail viewing, selection, and search-row interaction.
/// </summary>
public interface IRequestDetailsTarget
{
    void ShowRequestDetails(SearchableTurn entry);
    void CloseRequestDetails();
    void NavigateToPreviousRequest();
    void NavigateToNextRequest();
    void SelectSearchTurn(SearchableTurn entry);
    void SearchRowTapped(SearchableTurn? entry);
    void SearchRowDoubleTapped(SearchableTurn? entry);
}

