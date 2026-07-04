using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServer.Client;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace McpServerManager.UI.Core.Tests.TestInfrastructure;

/// <summary>
/// Mock-only harness for ViewModel CQRS dispatch verification (Byrd compliant, per strategist).
/// Constructs VMs using Substitute.For<Dispatcher>() directly.
/// Bans AddCqrs / AddUiCore / ServiceProvider.GetRequiredService<Dispatcher>() in dispatch-verification tests.
/// </summary>
public static class ViewModelDispatchTestHelper
{
    /// <summary>
    /// Creates a ChatWindowViewModel under test with mocked dispatcher.
    /// Caller configures dispatcher.QueryAsync/SendAsync .Returns(...) before calling VM actions.
    /// </summary>
    public static (IDispatcher dispatcher, ChatWindowViewModel vm) CreateChatWindow(
        Func<string>? getContext = null,
        string? initialModel = null,
        Action<string?>? onModelChanged = null,
        IUiDispatcherService? uiDispatcher = null)
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var vm = new ChatWindowViewModel(
            dispatcher,
            getContext ?? (() => string.Empty),
            initialModel,
            onModelChanged,
            ui);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Creates ConnectionViewModel with mocked dispatcher.
    /// </summary>
    public static (IDispatcher dispatcher, ConnectionViewModel vm) CreateConnection(
        IUiDispatcherService? uiDispatcher = null,
        ILogger<ConnectionViewModel>? logger = null)
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var log = logger ?? NullLogger<ConnectionViewModel>.Instance;
        var vm = new ConnectionViewModel(
            dispatcher,
            log,
            ui);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Helper to setup a canned query result.
    /// </summary>
    public static void SetupQueryResult<TMessage, TResult>(IDispatcher dispatcher, TResult value)
        where TMessage : IQuery<TResult>
    {
        dispatcher.QueryAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }

    /// <summary>
    /// Helper to setup a canned send result.
    /// </summary>
    public static void SetupSendResult<TMessage, TResult>(IDispatcher dispatcher, TResult value)
        where TMessage : ICommand<TResult>
    {
        dispatcher.SendAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }

    /// <summary>
    /// Verifies exact message was sent (for dispatch tests).
    /// </summary>
    public static async Task ReceivedQuery<TMessage, TResult>(IDispatcher dispatcher, int count = 1)
        where TMessage : IQuery<TResult>
    {
        await dispatcher.Received(count).QueryAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>());
    }

    public static async Task ReceivedSend<TMessage, TResult>(IDispatcher dispatcher, int count = 1)
        where TMessage : ICommand<TResult>
    {
        await dispatcher.Received(count).SendAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Invokes protected or non-public async method on VM instance by name.
    /// Test code calls the real VM entry surface only (no direct dispatcher setup in test bodies).
    /// Reflection confined to helper.
    /// </summary>
    public static async Task InvokeProtectedAsync(object vm, string methodName, params object?[]? args)
    {
        if (vm == null) throw new ArgumentNullException(nameof(vm));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var mi = vm.GetType().GetMethod(methodName, flags);
        if (mi == null)
            throw new MissingMethodException(vm.GetType().FullName, methodName);
        var result = mi.Invoke(vm, args ?? Array.Empty<object?>());
        if (result is Task t)
            await t.ConfigureAwait(true);
        else if (result != null)
        {
            var rt = result.GetType();
            if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // await via dynamic for generic task if needed in future
                await (dynamic)result;
            }
        }
    }

    /// <summary>
    /// Convenience: invoke SendAsync on Chat VM (protected).
    /// </summary>
    public static Task InvokeSendAsync(ChatWindowViewModel vm) => InvokeProtectedAsync(vm, "SendAsync");

    /// <summary>
    /// Convenience: invoke SubmitPromptAsync on Chat VM.
    /// </summary>
    public static Task InvokeSubmitPromptAsync(ChatWindowViewModel vm, PromptTemplate? prompt) => InvokeProtectedAsync(vm, "SubmitPromptAsync", prompt);

    /// <summary>
    /// Convenience: invoke PopulatePrompt on Chat VM.
    /// </summary>
    public static Task InvokePopulatePrompt(ChatWindowViewModel vm, PromptTemplate? prompt) => InvokeProtectedAsync(vm, "PopulatePrompt", prompt);

    // NOTE: Workspace ctor requires many collaborators (child VMs, timer, clipboard). Add specialized factory + invokes when a thin slice for it is ready.

    /// <summary>
    /// Creates WorkspaceViewModel with all deps substituted for pure dispatch verification.
    /// </summary>
    public static (IDispatcher dispatcher, WorkspaceViewModel vm) CreateWorkspace(IUiDispatcherService? uiDispatcher = null, ILogger<WorkspaceViewModel>? logger = null)
    {
        var dispatcherSub = Substitute.For<IDispatcher>();
        // The VM ctor takes concrete Dispatcher - adapt by wrapping if needed or assume test DI uses interface where possible.
        // For dispatch tests we pass a test double that the VM can use as its _dispatcher.
        // Use the interface version if VM accepts or cast; here we will new with fakes and override.
        var disp = dispatcherSub; // IDispatcher; VM field is Dispatcher - may require concrete impl or source change. For now use as-is via test shim.
        var clip = Substitute.For<IClipboardService>();
        var detail = Substitute.For<McpServerManager.UI.Core.ViewModels.WorkspaceDetailViewModel>();
        var global = Substitute.For<McpServerManager.UI.Core.ViewModels.WorkspaceGlobalPromptViewModel>();
        var health = Substitute.For<McpServerManager.UI.Core.ViewModels.WorkspaceHealthProbeViewModel>();
        var timer = Substitute.For<ITimerService>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var log = logger ?? NullLogger<WorkspaceViewModel>.Instance;

        // Note: ctor signature takes Dispatcher (likely concrete). We attempt construction with interface cast if assignable in runtime; otherwise tests may need adjustment or VM to take IDispatcher.
        WorkspaceViewModel vm;
        try
        {
            vm = new WorkspaceViewModel(clip, detail, global, health, timer, ui, (dynamic)disp, log);
        }
        catch
        {
            // Fallback: many tests use full DI; for pure dispatch we will skip full ctor in some cases or mark.
            throw new InvalidOperationException("WorkspaceViewModel ctor not interface-friendly for pure sub. Extend later or use integration dispatch test.");
        }
        return (dispatcherSub, vm);
    }

    public static Task InvokeSaveEditorAsync(WorkspaceViewModel vm) => InvokeProtectedAsync(vm, "SaveEditorAsync");

    /// <summary>
    /// Creates MainWindowViewModel using the protected hostServices ctor for pure dispatch verification of thin path.
    /// Hides ctor complexity and creation logic from test body.
    /// </summary>
    public static (IDispatcher dispatcher, MainWindowViewModel vm) CreateMainWindowWithHostServices(
        MainWindowHostServices hostServices,
        IClipboardService? clipboardService = null,
        IUiDispatcherService? uiDispatcher = null,
        ISystemNotificationService? systemNotificationService = null)
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var clip = clipboardService ?? Substitute.For<IClipboardService>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var sys = systemNotificationService ?? NoOpSystemNotificationService.Instance;
        // Use reflection to call protected ctor (hidden in helper per previous pattern for complex ctors).
        var ctor = typeof(MainWindowViewModel).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length == 4 && c.GetParameters()[1].ParameterType == typeof(MainWindowHostServices));
        if (ctor == null) throw new InvalidOperationException("Protected hostServices ctor not found");
        var vm = (MainWindowViewModel)ctor.Invoke(new object[] { clip, hostServices, sys, ui });
        // For dispatch verification, tests will call public methods after; dispatcher can be swapped if exposed, or test via state.
        return (dispatcher, vm);
    }

    /// <summary>
    /// Creates a minimal mock MainWindowHostServices for tests (no real services created).
    /// Uses uninitialized for sealed types to avoid proxy issues.
    /// </summary>
    public static MainWindowHostServices CreateMockMainWindowHostServices()
    {
        var idp = Substitute.For<IHostIdentityProvider>();
        var ctx = Substitute.For<IMcpHostContext>();
        // For sealed McpServerClient, use uninitialized object to satisfy ctor without real behavior (test only verifies thin ctor path).
#pragma warning disable SYSLIB0050
        var client = (McpServerClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpServerClient));
        var todo = (McpTodoService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpTodoService));
        var ws = (McpWorkspaceService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpWorkspaceService));
        var voice = (McpVoiceConversationService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpVoiceConversationService));
        var sess = (McpSessionLogService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpSessionLogService));
        var ev = (McpAgentEventStreamService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpAgentEventStreamService));
#pragma warning restore SYSLIB0050
        // Construct runtime legitimately with minimal provider so VM's InitializeMcpEndpoint (real shipped path) can call GetRequiredService without NRE.
        // Use uninitialized for sealed Dispatcher.
#pragma warning disable SYSLIB0050
        var fakeDispatcher = (McpServer.Cqrs.Dispatcher)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(McpServer.Cqrs.Dispatcher));
#pragma warning restore SYSLIB0050
        var services = new ServiceCollection();
        services.AddSingleton<McpServer.Cqrs.Dispatcher>(fakeDispatcher);
        var provider = services.BuildServiceProvider();
#pragma warning disable SYSLIB0050
        var wsContext = (WorkspaceContextViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorkspaceContextViewModel));
#pragma warning restore SYSLIB0050
        var runtime = new UiCoreHostRuntime(provider, wsContext);
        return new MainWindowHostServices(
            "http://localhost:7147",
            "mock-key",
            null,
            idp,
            ctx,
            client,
            todo,
            ws,
            voice,
            sess,
            ev,
            runtime);
    }

    /// <summary>
    /// Wires a test dispatcher substitute into the VM internal field (reflection only inside helper).
    /// Enables dispatch verification on MainWindow methods after creation via host path.
    /// </summary>
    public static void WireTestDispatcher(MainWindowViewModel vm, IDispatcher testDispatcher)
    {
        if (vm == null) throw new ArgumentNullException(nameof(vm));
        var field = typeof(MainWindowViewModel).GetField("_dispatcher", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("_dispatcher field not found for wiring in test helper");
        field.SetValue(vm, testDispatcher);
    }

    /// <summary>
    /// Creates MainWindowViewModel using real ctor path + mock host, then wires a fresh IDispatcher substitute.
    /// Use this for dispatch tests: setup results on returned dispatcher, call VM entry via direct or InvokeProtectedAsync, assert received + state apply.
    /// </summary>
    public static (IDispatcher dispatcher, MainWindowViewModel vm) CreateMainWindowForDispatchTests(
        IClipboardService? clipboardService = null,
        IUiDispatcherService? uiDispatcher = null,
        ISystemNotificationService? systemNotificationService = null)
    {
        var mockHost = CreateMockMainWindowHostServices();
        var (unused, vm) = CreateMainWindowWithHostServices(mockHost, clipboardService, uiDispatcher, systemNotificationService);
        var dispatcher = Substitute.For<IDispatcher>();
        WireTestDispatcher(vm, dispatcher);
        return (dispatcher, vm);
    }
}