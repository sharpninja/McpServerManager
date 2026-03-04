using System;
using System.Linq;
using McpServer.UI.Core.Messages;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

internal static class UiCoreMessageMapper
{
    public static ListTodosResult ToListTodosResult(McpTodoQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Items
            .Select(ToTodoListItem)
            .ToList();

        return new ListTodosResult(items, result.TotalCount);
    }

    public static TodoListItem ToTodoListItem(McpTodoFlatItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new TodoListItem(
            Id: item.Id,
            Title: item.Title,
            Section: item.Section,
            Priority: item.Priority,
            Done: item.Done,
            Estimate: item.Estimate);
    }

    public static TodoDetail ToTodoDetail(McpTodoFlatItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new TodoDetail(
            Id: item.Id,
            Title: item.Title,
            Section: item.Section,
            Priority: item.Priority,
            Done: item.Done,
            Estimate: item.Estimate,
            Note: item.Note,
            Description: item.Description?.ToList() ?? [],
            TechnicalDetails: item.TechnicalDetails?.ToList() ?? [],
            ImplementationTasks: item.ImplementationTasks?.Select(ToTodoTaskDetail).ToList() ?? [],
            CompletedDate: item.CompletedDate,
            DoneSummary: item.DoneSummary,
            Remaining: item.Remaining,
            PriorityNote: item.PriorityNote,
            Reference: item.Reference,
            DependsOn: item.DependsOn?.ToList() ?? [],
            FunctionalRequirements: item.FunctionalRequirements?.ToList() ?? [],
            TechnicalRequirements: item.TechnicalRequirements?.ToList() ?? []);
    }

    public static McpTodoFlatItem ToMcpTodoFlatItem(TodoDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new McpTodoFlatItem
        {
            Id = detail.Id,
            Title = detail.Title,
            Section = detail.Section,
            Priority = detail.Priority,
            Done = detail.Done,
            Estimate = detail.Estimate,
            Note = detail.Note,
            Description = detail.Description.ToList(),
            TechnicalDetails = detail.TechnicalDetails.ToList(),
            ImplementationTasks = detail.ImplementationTasks.Select(ToMcpTodoFlatTask).ToList(),
            CompletedDate = detail.CompletedDate,
            DoneSummary = detail.DoneSummary,
            Remaining = detail.Remaining,
            PriorityNote = detail.PriorityNote,
            Reference = detail.Reference,
            DependsOn = detail.DependsOn.ToList(),
            FunctionalRequirements = detail.FunctionalRequirements.ToList(),
            TechnicalRequirements = detail.TechnicalRequirements.ToList()
        };
    }

    public static TodoMutationOutcome ToTodoMutationOutcome(McpTodoMutationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new TodoMutationOutcome(
            Success: result.Success,
            Error: result.Error,
            Item: result.Item is null ? null : ToTodoDetail(result.Item));
    }

    public static TodoRequirementsAnalysis ToTodoRequirementsAnalysis(McpRequirementsAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new TodoRequirementsAnalysis(
            Success: result.Success,
            FunctionalRequirements: result.FunctionalRequirements?.ToList() ?? [],
            TechnicalRequirements: result.TechnicalRequirements?.ToList() ?? [],
            Error: result.Error,
            CopilotResponse: result.CopilotResponse);
    }

    public static McpTodoCreateRequest ToTodoCreateRequest(CreateTodoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new McpTodoCreateRequest
        {
            Id = command.Id,
            Title = command.Title,
            Section = command.Section,
            Priority = command.Priority,
            Estimate = command.Estimate,
            Note = command.Note,
            Remaining = command.Remaining,
            Description = command.Description?.ToList(),
            TechnicalDetails = command.TechnicalDetails?.ToList(),
            ImplementationTasks = command.ImplementationTasks?.Select(ToMcpTodoFlatTask).ToList(),
            DependsOn = command.DependsOn?.ToList(),
            FunctionalRequirements = command.FunctionalRequirements?.ToList(),
            TechnicalRequirements = command.TechnicalRequirements?.ToList()
        };
    }

    public static McpTodoUpdateRequest ToTodoUpdateRequest(UpdateTodoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new McpTodoUpdateRequest
        {
            Title = command.Title,
            Section = command.Section,
            Priority = command.Priority,
            Done = command.Done,
            Estimate = command.Estimate,
            Note = command.Note,
            CompletedDate = command.CompletedDate,
            DoneSummary = command.DoneSummary,
            Remaining = command.Remaining,
            Description = command.Description?.ToList(),
            TechnicalDetails = command.TechnicalDetails?.ToList(),
            ImplementationTasks = command.ImplementationTasks?.Select(ToMcpTodoFlatTask).ToList(),
            DependsOn = command.DependsOn?.ToList(),
            FunctionalRequirements = command.FunctionalRequirements?.ToList(),
            TechnicalRequirements = command.TechnicalRequirements?.ToList()
        };
    }

    public static ListWorkspacesResult ToListWorkspacesResult(McpWorkspaceQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Items
            .Where(static item => !string.IsNullOrWhiteSpace(item.WorkspacePath))
            .Select(ToWorkspaceSummary)
            .ToList();

        return new ListWorkspacesResult(items, result.TotalCount);
    }

    public static WorkspaceSummary ToWorkspaceSummary(McpWorkspaceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WorkspaceSummary(
            WorkspacePath: item.WorkspacePath ?? string.Empty,
            Name: item.Name ?? string.Empty,
            IsPrimary: item.IsPrimary ?? false,
            IsEnabled: item.IsEnabled ?? true);
    }

    public static WorkspaceDetail ToWorkspaceDetail(McpWorkspaceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WorkspaceDetail(
            WorkspacePath: item.WorkspacePath ?? string.Empty,
            Name: item.Name ?? string.Empty,
            TodoPath: item.TodoPath ?? string.Empty,
            DataDirectory: item.DataDirectory,
            TunnelProvider: item.TunnelProvider,
            IsPrimary: item.IsPrimary ?? false,
            IsEnabled: item.IsEnabled ?? true,
            RunAs: item.RunAs,
            PromptTemplate: item.PromptTemplate,
            StatusPrompt: item.StatusPrompt ?? string.Empty,
            ImplementPrompt: item.ImplementPrompt ?? string.Empty,
            PlanPrompt: item.PlanPrompt ?? string.Empty,
            DateTimeCreated: item.DateTimeCreated ?? DateTimeOffset.MinValue,
            DateTimeModified: item.DateTimeModified ?? item.DateTimeCreated ?? DateTimeOffset.MinValue,
            BannedLicenses: item.BannedLicenses?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList() ?? [],
            BannedCountriesOfOrigin: item.BannedCountriesOfOrigin?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList() ?? [],
            BannedOrganizations: item.BannedOrganizations?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList() ?? [],
            BannedIndividuals: item.BannedIndividuals?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList() ?? []);
    }

    public static WorkspaceMutationOutcome ToWorkspaceMutationOutcome(McpWorkspaceMutationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WorkspaceMutationOutcome(
            Success: result.Success,
            Error: result.Error,
            Item: result.Workspace is null ? null : ToWorkspaceDetail(result.Workspace));
    }

    public static WorkspaceProcessState ToWorkspaceProcessState(McpWorkspaceProcessStatus result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WorkspaceProcessState(
            IsRunning: result.IsRunning,
            Pid: result.Pid,
            Uptime: result.Uptime,
            Port: result.Port,
            Error: result.Error);
    }

    public static WorkspaceHealthState ToWorkspaceHealthState(McpWorkspaceHealthResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WorkspaceHealthState(
            Success: result.Success,
            StatusCode: result.StatusCode,
            Url: result.Url,
            Body: result.Body,
            Error: result.Error);
    }

    public static WorkspaceGlobalPromptState ToWorkspaceGlobalPromptState(McpWorkspaceGlobalPromptResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WorkspaceGlobalPromptState(
            Template: result.Template ?? string.Empty,
            IsDefault: result.IsDefault);
    }

    public static McpWorkspaceCreateRequest ToWorkspaceCreateRequest(CreateWorkspaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new McpWorkspaceCreateRequest
        {
            WorkspacePath = command.WorkspacePath,
            Name = command.Name,
            TodoPath = command.TodoPath,
            DataDirectory = command.DataDirectory,
            TunnelProvider = command.TunnelProvider,
            RunAs = command.RunAs,
            IsPrimary = command.IsPrimary,
            IsEnabled = command.IsEnabled,
            PromptTemplate = command.PromptTemplate,
            StatusPrompt = command.StatusPrompt,
            ImplementPrompt = command.ImplementPrompt,
            PlanPrompt = command.PlanPrompt,
            BannedLicenses = command.BannedLicenses?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedCountriesOfOrigin = command.BannedCountriesOfOrigin?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedOrganizations = command.BannedOrganizations?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedIndividuals = command.BannedIndividuals?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList()
        };
    }

    public static McpWorkspaceUpdateRequest ToWorkspaceUpdateRequest(UpdateWorkspaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new McpWorkspaceUpdateRequest
        {
            Name = command.Name,
            TodoPath = command.TodoPath,
            DataDirectory = command.DataDirectory,
            TunnelProvider = command.TunnelProvider,
            RunAs = command.RunAs,
            IsPrimary = command.IsPrimary,
            IsEnabled = command.IsEnabled,
            PromptTemplate = command.PromptTemplate,
            StatusPrompt = command.StatusPrompt,
            ImplementPrompt = command.ImplementPrompt,
            PlanPrompt = command.PlanPrompt,
            BannedLicenses = command.BannedLicenses?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedCountriesOfOrigin = command.BannedCountriesOfOrigin?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedOrganizations = command.BannedOrganizations?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList(),
            BannedIndividuals = command.BannedIndividuals?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList()
        };
    }

    private static TodoTaskDetail ToTodoTaskDetail(McpTodoFlatTask task)
        => new(task.Task ?? string.Empty, task.Done);

    private static McpTodoFlatTask ToMcpTodoFlatTask(TodoTaskDetail task)
        => new() { Task = task.Task, Done = task.Done };
}
