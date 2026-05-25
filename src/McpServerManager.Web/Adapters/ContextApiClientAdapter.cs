using McpServer.Client.Models;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Adapters;

internal sealed class ContextApiClientAdapter : IContextApiClient
{
    private readonly WebMcpContext _context;

    public ContextApiClientAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public async Task<ContextSearchPayload> SearchAsync(SearchContextQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Context.SearchAsync(
                    query.Query,
                    query.SourceType,
                    query.Limit <= 0 ? 20 : query.Limit,
                    ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new ContextSearchPayload(
            result.Query,
            result.Chunks.Select(MapChunk).ToList(),
            result.SourceKeys?.ToList() ?? []);
    }

    public async Task<ContextPackPayload> PackAsync(PackContextQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Context.PackAsync(
                    query.Query,
                    query.QueryId,
                    query.Limit <= 0 ? 20 : query.Limit,
                    ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new ContextPackPayload(
            result.QueryId,
            result.Chunks.Select(MapChunk).ToList(),
            result.SourceKeys?.ToList() ?? []);
    }

    public async Task<ContextSourcesPayload> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.UseActiveWorkspaceApiClientAsync(
                static (client, ct) => client.Context.ListSourcesAsync(ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new ContextSourcesPayload(
            result.Sources.Select(s => new ContextSourceView(s.SourceKey, s.SourceType, s.IngestedAt)).ToList());
    }

    public async Task<ContextRebuildResult> RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.UseActiveWorkspaceApiClientAsync(
                static (client, ct) => client.Context.RebuildIndexAsync(ct),
                cancellationToken)
            .ConfigureAwait(true);
        return new ContextRebuildResult(result.Status, DateTimeOffset.UtcNow);
    }

    private static ContextChunkView MapChunk(ContextChunkResult c)
        => new(c.Id, c.DocumentId, c.Content, c.TokenCount, c.ChunkIndex, c.Score);
}
