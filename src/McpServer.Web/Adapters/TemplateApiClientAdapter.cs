using System.Text.Json;
using McpServer.Client;
using McpServer.Client.Models;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServer.Web.Adapters;

internal sealed class TemplateApiClientAdapter : ITemplateApiClient
{
    private readonly WebMcpContext _context;
    private readonly ILogger<TemplateApiClientAdapter> _logger;

    public TemplateApiClientAdapter(WebMcpContext context, ILogger<TemplateApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<TemplateApiClientAdapter>.Instance;
    }

    public async Task<ListTemplatesResult> ListTemplatesAsync(
        string? category, string? tag, string? keyword,
        CancellationToken cancellationToken = default)
    {
        var response = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => client.Template.QueryAsync(category, tag, keyword, ct),
                cancellationToken)
            .ConfigureAwait(true);

        var items = response.Items
            .Select(i => new TemplateListItem(i.Id, i.Title, i.Category, i.Tags.ToList(), i.Description))
            .ToList();

        return new ListTemplatesResult(items, response.TotalCount);
    }

    public async Task<TemplateDetail?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Template.GetAsync(templateId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MapDetail(item);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<TemplateMutationOutcome> CreateTemplateAsync(
        CreateTemplateCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Template.CreateAsync(new TemplateCreateRequest
                    {
                        Id = command.Id,
                        Title = command.Title,
                        Category = command.Category,
                        Content = command.Content,
                        Tags = command.Tags?.ToList(),
                        Description = command.Description,
                        Engine = command.Engine,
                    }, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return MapMutationOutcome(result);
        }
        catch (McpConflictException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new TemplateMutationOutcome(false, ex.Message, null);
        }
        catch (McpValidationException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new TemplateMutationOutcome(false, ex.Message, null);
        }
    }

    public async Task<TemplateMutationOutcome> UpdateTemplateAsync(
        UpdateTemplateCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Template.UpdateAsync(command.TemplateId, new TemplateUpdateRequest
                    {
                        Title = command.Title,
                        Category = command.Category,
                        Content = command.Content,
                        Tags = command.Tags?.ToList(),
                        Description = command.Description,
                        Engine = command.Engine,
                    }, ct),
                    cancellationToken)
                .ConfigureAwait(true);

            return MapMutationOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new TemplateMutationOutcome(false, ex.Message, null);
        }
        catch (McpValidationException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new TemplateMutationOutcome(false, ex.Message, null);
        }
    }

    public async Task<TemplateMutationOutcome> DeleteTemplateAsync(
        string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.UseActiveWorkspaceApiClientAsync(
                    (client, ct) => client.Template.DeleteAsync(templateId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MapMutationOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new TemplateMutationOutcome(false, ex.Message, null);
        }
    }

    public async Task<TemplateTestOutcome> TestTemplateAsync(
        TestTemplateQuery query, CancellationToken cancellationToken = default)
    {
        var variables = string.IsNullOrWhiteSpace(query.VariablesJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(query.VariablesJson) ?? new();

        var request = new TemplateTestRequest { Variables = variables, InlineTemplate = query.InlineTemplate };

        var result = await _context.UseActiveWorkspaceApiClientAsync(
                (client, ct) => !string.IsNullOrWhiteSpace(query.TemplateId)
                    ? client.Template.TestAsync(query.TemplateId, request, ct)
                    : client.Template.TestInlineAsync(request, ct),
                cancellationToken)
            .ConfigureAwait(true);

        return new TemplateTestOutcome(
            result.Success,
            result.RenderedContent,
            result.Error,
            result.MissingVariables?.ToList());
    }

    private static TemplateDetail? MapDetail(TemplateItem? item)
    {
        if (item is null)
            return null;

        return new TemplateDetail(
            item.Id,
            item.Title,
            item.Category,
            item.Tags.ToList(),
            item.Description,
            item.Engine,
            item.Variables.Select(v => new TemplateVariableDetail(
                v.Name, v.Description, v.Required, v.Example, v.DefaultValue)).ToList(),
            item.Content);
    }

    private static TemplateMutationOutcome MapMutationOutcome(TemplateMutationResult result)
    {
        return new TemplateMutationOutcome(
            result.Success,
            result.Error,
            MapDetail(result.Item));
    }
}
