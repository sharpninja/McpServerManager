using McpServer.Client;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServer.Web.Adapters;

internal sealed class RepoApiClientAdapter : IRepoApiClient
{
    private readonly WebMcpContext _context;
    private readonly ILogger<RepoApiClientAdapter> _logger;

    public RepoApiClientAdapter(WebMcpContext context, ILogger<RepoApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<RepoApiClientAdapter>.Instance;
    }

    public async Task<RepoListResultView> ListAsync(ListRepoEntriesQuery query, CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Repo.ListAsync(query.Path, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new RepoListResultView(
            response.Path,
            response.Entries.Select(item => new RepoEntrySummary(item.Name, item.IsDirectory)).ToList());
    }

    public async Task<RepoFileDetail> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Repo.ReadFileAsync(path, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return new RepoFileDetail(response.Path, response.Content, response.Exists);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new RepoFileDetail(path, null, false);
        }
    }

    public async Task<RepoWriteOutcome> WriteFileAsync(WriteRepoFileCommand command, CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Repo.WriteFileAsync(command.Path, command.Content, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new RepoWriteOutcome(response.Path, response.Written);
    }
}
