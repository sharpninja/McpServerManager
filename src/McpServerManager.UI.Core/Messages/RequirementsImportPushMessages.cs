using System.Collections.Generic;
using McpServer.Cqrs;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Target for publishing an exported requirements wiki (PLAN-REQSDESKTOP-001, FR-REQS-PUSH-001).</summary>
public enum WikiPushTarget
{
    /// <summary>Publish to a GitHub wiki/repository.</summary>
    GitHub,

    /// <summary>Publish to an Azure DevOps wiki/repository.</summary>
    Azure,
}

/// <summary>Result of a wiki push (FR-REQS-PUSH-001).</summary>
/// <param name="Success">Whether the push succeeded.</param>
/// <param name="Error">Error message when the push failed.</param>
/// <param name="Location">The published location/URL when available.</param>
public sealed record WikiPushResult(bool Success, string? Error, string? Location);

/// <summary>
/// Generates the requirements wiki and pushes it to the given target (FR-REQS-PUSH-001).
/// </summary>
/// <param name="Target">Where to publish.</param>
/// <param name="Doc">Document selector passed to generation (default all).</param>
public sealed record PushRequirementsWikiCommand(WikiPushTarget Target, string Doc = "all")
    : ICommand<WikiPushResult>;

/// <summary>A parsed set of requirements to import (PLAN-REQSDESKTOP-001, FR-REQS-IMPORT-001).</summary>
/// <param name="Functional">Functional requirement create commands.</param>
/// <param name="Technical">Technical requirement create commands.</param>
/// <param name="Testing">Testing requirement create commands.</param>
public sealed record RequirementsImportRequest(
    IReadOnlyList<CreateFunctionalRequirementCommand> Functional,
    IReadOnlyList<CreateTechnicalRequirementCommand> Technical,
    IReadOnlyList<CreateTestingRequirementCommand> Testing);

/// <summary>Outcome of a requirements import (FR-REQS-IMPORT-001).</summary>
/// <param name="FunctionalCreated">Count of functional requirements created.</param>
/// <param name="TechnicalCreated">Count of technical requirements created.</param>
/// <param name="TestingCreated">Count of testing requirements created.</param>
/// <param name="Errors">Per-item error messages.</param>
public sealed record RequirementsImportResult(
    int FunctionalCreated,
    int TechnicalCreated,
    int TestingCreated,
    IReadOnlyList<string> Errors);

/// <summary>Imports a parsed set of requirements, creating each record (FR-REQS-IMPORT-001).</summary>
/// <param name="Request">The parsed requirements to create.</param>
public sealed record ImportRequirementsCommand(RequirementsImportRequest Request)
    : ICommand<RequirementsImportResult>;
