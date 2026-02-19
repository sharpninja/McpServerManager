using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using RequestTracker.Models;
using RequestTracker.Models.Json;

namespace RequestTracker.Services;

public sealed class McpSessionLogService
{
    private const int PageSize = 1000;
    private readonly HttpClient _httpClient;

    public McpSessionLogService(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<UnifiedSessionLog>> GetAllSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = new List<UnifiedSessionLog>();
        var offset = 0;
        var total = int.MaxValue;

        while (offset < total)
        {
            var url = $"/mcp/sessionlog?limit={PageSize}&offset={offset}";
            var page = await _httpClient.GetFromJsonAsync<McpSessionLogQueryResult>(url, cancellationToken).ConfigureAwait(false);
            if (page == null || page.Items == null || page.Items.Count == 0)
                break;

            total = page.TotalCount;
            foreach (var item in page.Items)
                sessions.Add(Map(item));

            offset += page.Items.Count;
            if (page.Items.Count < PageSize)
                break;
        }

        return sessions;
    }

    private static UnifiedSessionLog Map(McpUnifiedSessionLogDto dto)
    {
        var log = new UnifiedSessionLog
        {
            SourceType = dto.SourceType ?? "",
            SessionId = dto.SessionId ?? "",
            Title = dto.Title ?? "",
            Model = dto.Model ?? "",
            Started = ParseDateTime(dto.Started),
            LastUpdated = ParseDateTime(dto.LastUpdated),
            Status = dto.Status ?? "",
            EntryCount = dto.EntryCount,
            TotalTokens = dto.TotalTokens ?? 0,
            CursorSessionLabel = dto.CursorSessionLabel,
            Workspace = dto.Workspace == null
                ? null
                : new WorkspaceInfo
                {
                    Project = dto.Workspace.Project ?? "",
                    TargetFramework = dto.Workspace.TargetFramework ?? "",
                    Repository = dto.Workspace.Repository ?? "",
                    Branch = dto.Workspace.Branch ?? ""
                },
            CopilotStatistics = dto.CopilotStatistics == null
                ? null
                : new StatisticsInfo
                {
                    AverageSuccessScore = dto.CopilotStatistics.AverageSuccessScore,
                    TotalNetTokens = dto.CopilotStatistics.TotalNetTokens,
                    TotalNetPremiumRequests = dto.CopilotStatistics.TotalNetPremiumRequests,
                    CompletedCount = dto.CopilotStatistics.CompletedCount,
                    InProgressCount = dto.CopilotStatistics.InProgressCount
                }
        };

        if (dto.Entries != null)
        {
            foreach (var entry in dto.Entries)
                log.Entries.Add(MapEntry(entry, log.SourceType));
        }

        if (log.EntryCount <= 0)
            log.EntryCount = log.Entries.Count;

        return log;
    }

    private static UnifiedRequestEntry MapEntry(McpUnifiedRequestEntryDto dto, string sourceType)
    {
        var entry = new UnifiedRequestEntry
        {
            RequestId = dto.RequestId ?? "",
            Timestamp = ParseDateTime(dto.Timestamp),
            Model = dto.Model ?? "",
            ModelProvider = dto.ModelProvider ?? "",
            Agent = sourceType ?? "",
            QueryText = dto.QueryText ?? "",
            QueryTitle = dto.QueryTitle ?? "",
            Response = dto.Response ?? "",
            Interpretation = dto.Interpretation ?? "",
            Status = dto.Status ?? "",
            ContextList = dto.ContextList ?? new List<string>(),
            RawContext = dto.RawContext,
            TokenCount = dto.TokenCount ?? 0,
            IsPremium = dto.IsPremium ?? false,
            Score = dto.Score,
            FailureNote = dto.FailureNote ?? "",
            OriginalEntry = dto.OriginalEntry ?? dto
        };

        if (dto.Tags != null && dto.Tags.Count > 0)
            entry.Tags.AddRange(dto.Tags);

        if (dto.Actions != null)
        {
            foreach (var action in dto.Actions)
            {
                entry.Actions.Add(new UnifiedAction
                {
                    Order = action.Order,
                    Description = action.Description ?? "",
                    Type = action.Type ?? "",
                    Status = action.Status ?? "",
                    FilePath = action.FilePath ?? ""
                });
            }
        }

        if (dto.ProcessingDialog != null)
        {
            foreach (var dialog in dto.ProcessingDialog)
            {
                entry.ProcessingDialog.Add(new UnifiedProcessingDialogItem
                {
                    Timestamp = dialog.Timestamp ?? "",
                    Role = dialog.Role ?? "",
                    Content = dialog.Content ?? "",
                    Category = dialog.Category ?? ""
                });
            }
        }

        return entry;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTimeOffset.TryParse(value, out var dto))
            return dto.UtcDateTime;
        if (DateTime.TryParse(value, out var dt))
            return dt;
        return null;
    }
}
