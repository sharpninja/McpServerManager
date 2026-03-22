# DI Convergence Matrix

This matrix captures the corrected steady state after `ARCH-REFACTOR-001`.
The hosts no longer use separate runtime/provider factory stacks; they converge
on the shared `AddMcpHost(...)` contract and differ only where host lifetime,
identity, or UI shell behavior genuinely require it.

## Shared invariants

| Area | Steady state |
| :--- | :--- |
| Canonical registration contract | `src/McpServer.UI.Core/Hosting/McpHostBuilderExtensions.cs` via `AddMcpHost(...)` |
| Shared leaf registration | `AddUiCore(...)` |
| Host identity abstraction | `IHostIdentityProvider` |
| Host context abstraction | `IMcpHostContext` |
| Dispatcher lifetime control | `McpHostLifetimeStrategy.Singleton` or `Scoped` |
| CQRS logging | Singleton hosts use `AddCqrsLoggerProvider()` through provider-aware logger factories |
| Legacy runtime/provider wrappers | Removed from `src/` |

## Host matrix

| Host | Host mode | Dispatcher lifetime | Composition root | Identity source | MCP context | Logging shape | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Desktop | Command-target | Singleton | `DesktopAppServiceFactory` builds per-connection session providers with `AddMcpHost(...)` | `AvaloniaHostIdentityProvider` | `AvaloniaMcpContext` | `AppLogService` plus `AddCqrsLoggerProvider()` | Main window command target is attached after construction through a deferred accessor. |
| Android | Command-target | Singleton | `AndroidAppServiceFactory` builds per-connection session providers with `AddMcpHost(...)` | `AvaloniaHostIdentityProvider` | `AvaloniaMcpContext` | `AppLogService` plus `AddCqrsLoggerProvider()` | Preserves Android-specific clipboard, notifications, and lifecycle adapters on top of the shared graph. |
| Web | Command-target | Scoped | `WebServiceRegistration.AddWebServices()` layers scoped Web overrides on `AddMcpHost(...)` | `WebHostIdentityProvider` | `WebMcpContext` | Scoped dispatcher surfaces stay in request scope; no singleton dispatcher logger provider | Keeps bearer-token forwarding and per-user workspace isolation. |
| Director | Command-target | Singleton | `DirectorServiceRegistration.Configure(...)` plus `DirectorHost.CreateProvider(...)` | `DirectorHostIdentityProvider` | `DirectorMcpContext` | `LoggerFactory.Create(...)` honoring DI providers plus Serilog file logging and `AddCqrsLoggerProvider()` | All five Director entry points now converge on `DirectorHost.CreateProvider(...)`. |
| VSIX Todo | Provider-only | Singleton | `McpServerMcpTodoToolWindowPane` uses `AddMcpHost(...)` without `ICommandTarget` | Solution path through `WorkspaceContextViewModel` | Shared workspace context only; no host command target | `AppLogService` shared pipeline | Keeps tool-window-owned `ServiceProvider` disposal and resolves `Dispatcher` directly from DI. |

## Logging convergence

| Concern | Final behavior |
| :--- | :--- |
| `AppLogService.AddProvider(...)` | Retains attached `ILoggerProvider` instances and fans out log writes to them |
| Singleton host CQRS log capture | Registered through `AddCqrsLoggerProvider()` rather than post-build `ILoggerFactory.AddProvider(...)` hacks |
| Director logger factory | Uses `LoggerFactory.Create(...)` over DI-provided providers, including Serilog |
| Web logging | Remains scoped and avoids introducing singleton/scoped cycles |

## Identity convergence

| Host family | Bearer token | API key | Workspace path |
| :--- | :--- | :--- | :--- |
| Desktop and Android | Mutable Avalonia session state | Mutable Avalonia session state | Active `WorkspaceContextViewModel` path |
| Web | `BearerTokenAccessor` | ASP.NET configuration | Scoped workspace context, then config fallback |
| Director | Cached CLI token state | Active or control client configuration | `DirectorMcpContext.ActiveWorkspacePath` |
| VSIX Todo | Not required for the todo-only host shape | Not required for the todo-only host shape | Active solution directory |
