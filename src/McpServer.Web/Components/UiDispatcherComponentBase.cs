using System.Threading.Tasks;
using McpServer.UI.Core.Services;
using Microsoft.AspNetCore.Components;

namespace McpServer.Web.Components;

public abstract class UiDispatcherComponentBase : ComponentBase
{
    [Inject]
    protected IUiDispatcherService UiDispatcher { get; set; } = default!;

    protected Task RefreshUiAsync()
        => UiDispatcher.InvokeAsync(() =>
        {
            StateHasChanged();
            return Task.CompletedTask;
        });
}
