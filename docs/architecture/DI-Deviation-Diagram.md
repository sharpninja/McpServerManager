# DI Convergence Diagram

This diagram reflects the post-refactor steady state for `ARCH-REFACTOR-001`.
All five hosts now compose their shared MCP/UI graph through `AddMcpHost(...)`,
while still preserving the two supported host shapes:

- Command-target hosts: Desktop, Android, Web, Director
- Provider-only hosts: VSIX todo tool window

```mermaid
graph TD
    AddMcpHost[AddMcpHost options]

    AddMcpHost --> Lifetime[McpHostLifetimeStrategy]
    AddMcpHost --> Identity[IHostIdentityProvider]
    AddMcpHost --> Context[IMcpHostContext]
    AddMcpHost --> Logging[Logging pipeline]
    AddMcpHost --> HostMode[Host mode]

    Lifetime --> Singleton[Singleton dispatcher]
    Lifetime --> Scoped[Scoped dispatcher]

    Identity --> AvaloniaIdentity[AvaloniaHostIdentityProvider]
    Identity --> WebIdentity[WebHostIdentityProvider]
    Identity --> DirectorIdentity[DirectorHostIdentityProvider]

    Context --> AvaloniaContext[AvaloniaMcpContext]
    Context --> WebContext[WebMcpContext]
    Context --> DirectorContext[DirectorMcpContext]

    Logging --> AppLog[AppLogService composite logger factory]
    Logging --> CqrsProvider[AddCqrsLoggerProvider]
    Logging --> Serilog[Director LoggerFactory plus Serilog provider]

    HostMode --> CommandTargetMode[Command-target host]
    HostMode --> ProviderOnlyMode[Provider-only host]

    CommandTargetMode --> Desktop[Desktop]
    CommandTargetMode --> Android[Android]
    CommandTargetMode --> Web[Web]
    CommandTargetMode --> Director[Director]
    ProviderOnlyMode --> Vsix[VSIX Todo pane]

    Desktop --> Singleton
    Android --> Singleton
    Director --> Singleton
    Web --> Scoped
    Vsix --> Singleton
```

## Notes

- `AddUiCore(...)` remains the leaf shared registration API underneath `AddMcpHost(...)`.
- Desktop and Android build per-connection session providers through their app service factories, then attach the main-window command target after construction.
- Web stays scoped to preserve bearer-token and workspace isolation per request/circuit.
- Director now has one provider entry point: `DirectorHost.CreateProvider(...)`.
- VSIX uses `AddMcpHost(...)` without an `ICommandTarget`, resolving `Dispatcher` directly from the built provider.
