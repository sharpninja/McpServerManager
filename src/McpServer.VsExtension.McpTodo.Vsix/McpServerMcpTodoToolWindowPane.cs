using System;
using System.Runtime.InteropServices;
using McpServer.Cqrs;
using McpServer.UI;
using McpServer.UI.Core;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;

namespace McpServer.VsExtension.McpTodo;

[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public sealed class McpServerMcpTodoToolWindowPane : ToolWindowPane
{
    private readonly IServiceProvider _serviceProvider;

    public McpServerMcpTodoToolWindowPane() : base(null)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        Caption = "MCP Todo";

        var dte = (EnvDTE.DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
        var solutionPath = dte?.Solution?.FullName;
        var solutionDir = string.IsNullOrWhiteSpace(solutionPath)
            ? null
            : System.IO.Path.GetDirectoryName(solutionPath);

        var client = new McpTodoClient(solutionDir: solutionDir);
        var editorService = TodoEditorService.Instance ?? new TodoEditorService(client);
        var uiDispatcher = new VsixUiDispatcherService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITodoApiClient>(_ => new VsixTodoApiClientAdapter(client));
        services.AddSingleton<IUiDispatcherService>(_ => uiDispatcher);
        services.AddSingleton<IClipboardService>(sp => new VsixClipboardService(sp.GetRequiredService<IUiDispatcherService>()));
        services.AddSingleton<ITimerService, NoOpTimerService>();
        services.AddCqrsDispatcher();
        services.AddUiCore();
        UiDispatcherHost.Configure(uiDispatcher);

        _serviceProvider = services.BuildServiceProvider();

        var viewModel = ActivatorUtilities.CreateInstance<TodoToolWindowViewModel>(
            _serviceProvider,
            editorService,
            (Action<string>)McpServerMcpTodoToolWindowControl.OpenFileInEditor,
            (Action<string, string>)McpServerMcpTodoToolWindowControl.ShowCompletionInfoBar);
        viewModel.ApplyWorkspacePath(solutionDir);

        Content = new McpServerMcpTodoToolWindowControl(viewModel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            (_serviceProvider as IDisposable)?.Dispose();

        base.Dispose(disposing);
    }
}
