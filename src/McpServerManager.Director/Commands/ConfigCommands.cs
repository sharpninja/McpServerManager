using System.CommandLine;
using Spectre.Console;
using static McpServerManager.Director.Commands.CommandHelpers;

namespace McpServerManager.Director.Commands;

/// <summary>
/// CLI configuration commands for Director defaults (e.g., default MCP server URL).
/// </summary>
internal static class ConfigCommands
{
    public static void Register(RootCommand root)
    {
        var config = new Command("config", "Manage Director CLI defaults (outside workspace marker files)");
        config.Subcommands.Add(BuildShowCommand());
        config.Subcommands.Add(BuildSetDefaultUrlCommand());
        config.Subcommands.Add(BuildClearDefaultUrlCommand());
        root.Subcommands.Add(config);
    }

    private static Command BuildShowCommand()
    {
        var cmd = new Command("show", "Show current Director CLI config");
        cmd.SetAction(_ =>
        {
            var cfg = DirectorCliConfigStore.Load();
            var table = new Table();
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("Config Path", Markup.Escape(DirectorCliConfigStore.GetConfigPath()));
            table.AddRow("Default Base URL", Markup.Escape(cfg.DefaultBaseUrl ?? "(not set)"));

            AnsiConsole.Write(table);
        });
        return cmd;
    }

    private static Command BuildSetDefaultUrlCommand()
    {
        var urlArg = new Argument<string>("url")
        {
            Description = "Default MCP server base URL (e.g. http://localhost:7147)",
        };
        var cmd = new Command("set-default-url", "Set default MCP server base URL for use outside a workspace")
        {
            urlArg,
        };
        cmd.Aliases.Add("set-url");

        cmd.SetAction(parseResult =>
        {
            var url = parseResult.GetValue(urlArg)!;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                Error("URL must be an absolute http:// or https:// URL.");
                return;
            }

            var normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            var cfg = DirectorCliConfigStore.Load();
            cfg.DefaultBaseUrl = normalized;
            DirectorCliConfigStore.Save(cfg);

            Success($"Default MCP base URL set to {normalized}");
            Info($"Saved at {DirectorCliConfigStore.GetConfigPath()}");
            Warn("Workspace-scoped commands still require a workspace marker/API key unless bearer-only support is added.");
        });

        return cmd;
    }

    private static Command BuildClearDefaultUrlCommand()
    {
        var cmd = new Command("clear-default-url", "Clear the default MCP server base URL");
        cmd.SetAction(_ =>
        {
            var cfg = DirectorCliConfigStore.Load();
            cfg.DefaultBaseUrl = null;
            DirectorCliConfigStore.Save(cfg);
            Success("Default MCP base URL cleared.");
            Info($"Config file: {DirectorCliConfigStore.GetConfigPath()}");
        });
        return cmd;
    }
}
