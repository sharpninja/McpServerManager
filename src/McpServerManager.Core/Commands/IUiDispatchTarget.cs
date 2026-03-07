using System;
using System.Threading.Tasks;

namespace McpServerManager.Core.Commands;

/// <summary>
/// UI thread dispatching and background work tracking.
/// </summary>
public interface IUiDispatchTarget : McpServer.UI.Core.Commands.IUiDispatchTarget
{
    new void DispatchToUi(Action action);
    new void TrackBackgroundWork(Task task);
    new string StatusMessage { get; set; }
}
