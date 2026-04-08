using System.Threading.Tasks;
using McpServerManager.UI.Core.Services;
using Microsoft.AspNetCore.Components;

namespace McpServerManager.Web.Components;

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
