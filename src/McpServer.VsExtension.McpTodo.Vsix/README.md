# McpServer MCP Todo – Visual Studio extension (VSIX)

This is the **Visual Studio VSIX project** for the MCP Todo tool window. It is built with the SDK-style project in this folder, while the repo-level packaging flow assembles the final `.vsix`.

## Building

**Build from the repo root**

1. Run `.\build.ps1 BuildAndInstallVsix --skip-install` from the repo root to build and package the VSIX without launching the installer.
2. The packaged VSIX is written to `bin\Release\net9.0-windows\win\McpServer.VsExtension.McpTodo.vsix`.
3. Omit `--skip-install` if you want the packaging flow to launch the VSIX installer after the package is created.

**From command line**

- Run `dotnet build src\McpServer.VsExtension.McpTodo.Vsix\McpServer.VsExtension.McpTodo.Vsix.csproj -c Release` to compile the assembly-only output.
- Use `.\build.ps1 BuildAndInstallVsix --skip-install` when you need the final `.vsix` package.

## After installing

In Visual Studio: **View > Other Windows > MCP Todo**. Ensure the MCP server is running (e.g. `.\scripts\Start-McpServer.ps1`) so the tool can load TODO items.
