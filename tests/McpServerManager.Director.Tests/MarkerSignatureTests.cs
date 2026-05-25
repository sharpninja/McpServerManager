using System.Security.Cryptography;
using System.Text;
using McpServerManager.Director.Commands;

namespace McpServerManager.Director.Tests;

public sealed class MarkerSignatureTests
{
    [Fact]
    public void VerifyMarkerSignature_AcceptsMarkerWithSignedAgentPluginContract()
    {
        var fields = CreateMarkerFields(includeAgentPluginContract: true);

        var verified = DirectorCommands.VerifyMarkerSignature(fields, out var error);

        Assert.True(verified, error);
    }

    [Fact]
    public void VerifyMarkerSignature_AcceptsLegacyMarkerWithoutAgentPluginContract()
    {
        var fields = CreateMarkerFields(includeAgentPluginContract: false);

        var verified = DirectorCommands.VerifyMarkerSignature(fields, out var error);

        Assert.True(verified, error);
    }

    [Fact]
    public void ParseMarkerFields_CapturesSignedAgentPluginContractFields()
    {
        var fields = DirectorCommands.ParseMarkerFields(
        [
            "port: 7147",
            "baseUrl: http://PAYTON-LEGION2:7147",
            "agent_plugins:",
            "  policy: required",
            "  contract_digest: ABC123",
            "  agents:",
            "    Codex:",
            "      plugin_name: mcpserver-codex-plugin",
            "signature:",
            "  canonicalization: marker-v1",
            "  value: 00",
        ]);

        Assert.Equal("required", fields["agent_plugins.policy"]);
        Assert.Equal("ABC123", fields["agent_plugins.contract_digest"]);
        Assert.False(fields.ContainsKey("agent_plugins.agents"));
    }

    private static Dictionary<string, string> CreateMarkerFields(bool includeAgentPluginContract)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["signature.canonicalization"] = "marker-v1",
            ["port"] = "7147",
            ["baseUrl"] = "http://PAYTON-LEGION2:7147",
            ["apiKey"] = "test-api-key",
            ["workspace"] = "aiUnit",
            ["workspacePath"] = @"F:\GitHub\aiUnit",
            ["pid"] = "12345",
            ["startedAt"] = "2026-05-24T22:47:51.0858862+00:00",
            ["markerWrittenAtUtc"] = "2026-05-24T22:47:51.0858862+00:00",
            ["serverStartedAtUtc"] = "2026-05-24T22:47:40.8372074+00:00",
            ["endpoints.health"] = "/health",
            ["endpoints.swagger"] = "/swagger/v1/swagger.json",
            ["endpoints.swaggerUi"] = "/swagger",
            ["endpoints.mcpTransport"] = "/mcp-transport",
            ["endpoints.sessionLog"] = "/mcpserver/sessionlog",
            ["endpoints.sessionLogDialog"] = "/mcpserver/sessionlog/{agent}/{sessionId}/{requestId}/dialog",
            ["endpoints.contextSearch"] = "/mcpserver/context/search",
            ["endpoints.contextPack"] = "/mcpserver/context/pack",
            ["endpoints.contextSources"] = "/mcpserver/context/sources",
            ["endpoints.todo"] = "/mcpserver/todo",
            ["endpoints.repo"] = "/mcpserver/repo",
            ["endpoints.desktop"] = "/mcpserver/desktop",
            ["endpoints.gitHub"] = "/mcpserver/gh",
            ["endpoints.tools"] = "/mcpserver/tools",
            ["endpoints.workspace"] = "/mcpserver/workspace",
            ["endpoints.serverStartupUtc"] = "/server-startup-utc",
            ["endpoints.markerFileTimestamp"] = "/marker-file-timestamp?repoPath={workspacePath}",
        };

        if (includeAgentPluginContract)
        {
            fields["agent_plugins.policy"] = "required";
            fields["agent_plugins.contract_digest"] = "9DD53CB0312DEC7A8D63676380E2904FDB47CE223F56333360E1F672A7F24EF5";
        }

        fields["signature.value"] = ComputeSignature(fields, includeAgentPluginContract);
        return fields;
    }

    private static string ComputeSignature(IReadOnlyDictionary<string, string> fields, bool includeAgentPluginContract)
    {
        var payload = new StringBuilder()
            .Append("canonicalization=").Append(fields["signature.canonicalization"]).Append('\n')
            .Append("port=").Append(fields["port"]).Append('\n')
            .Append("baseUrl=").Append(fields["baseUrl"]).Append('\n')
            .Append("apiKey=").Append(fields["apiKey"]).Append('\n')
            .Append("workspace=").Append(fields["workspace"]).Append('\n')
            .Append("workspacePath=").Append(fields["workspacePath"]).Append('\n')
            .Append("pid=").Append(fields["pid"]).Append('\n')
            .Append("startedAt=").Append(fields["startedAt"]).Append('\n')
            .Append("markerWrittenAtUtc=").Append(fields["markerWrittenAtUtc"]).Append('\n')
            .Append("serverStartedAtUtc=").Append(fields["serverStartedAtUtc"]).Append('\n')
            .Append("endpoints.health=").Append(fields["endpoints.health"]).Append('\n')
            .Append("endpoints.swagger=").Append(fields["endpoints.swagger"]).Append('\n')
            .Append("endpoints.swaggerUi=").Append(fields["endpoints.swaggerUi"]).Append('\n')
            .Append("endpoints.mcpTransport=").Append(fields["endpoints.mcpTransport"]).Append('\n')
            .Append("endpoints.sessionLog=").Append(fields["endpoints.sessionLog"]).Append('\n')
            .Append("endpoints.sessionLogDialog=").Append(fields["endpoints.sessionLogDialog"]).Append('\n')
            .Append("endpoints.contextSearch=").Append(fields["endpoints.contextSearch"]).Append('\n')
            .Append("endpoints.contextPack=").Append(fields["endpoints.contextPack"]).Append('\n')
            .Append("endpoints.contextSources=").Append(fields["endpoints.contextSources"]).Append('\n')
            .Append("endpoints.todo=").Append(fields["endpoints.todo"]).Append('\n')
            .Append("endpoints.repo=").Append(fields["endpoints.repo"]).Append('\n')
            .Append("endpoints.desktop=").Append(fields["endpoints.desktop"]).Append('\n')
            .Append("endpoints.gitHub=").Append(fields["endpoints.gitHub"]).Append('\n')
            .Append("endpoints.tools=").Append(fields["endpoints.tools"]).Append('\n')
            .Append("endpoints.workspace=").Append(fields["endpoints.workspace"]).Append('\n')
            .Append("endpoints.serverStartupUtc=").Append(fields["endpoints.serverStartupUtc"]).Append('\n')
            .Append("endpoints.markerFileTimestamp=").Append(fields["endpoints.markerFileTimestamp"]).Append('\n');

        if (includeAgentPluginContract)
        {
            payload.Append("agentPlugins.policy=").Append(fields["agent_plugins.policy"]).Append('\n')
                .Append("agentPlugins.contractDigest=").Append(fields["agent_plugins.contract_digest"]).Append('\n');
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(fields["apiKey"]));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }
}
