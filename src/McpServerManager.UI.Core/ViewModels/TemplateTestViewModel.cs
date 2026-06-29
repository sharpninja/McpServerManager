using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// ViewModel for testing templates by ID or inline content.
/// </summary>
public sealed partial class TemplateTestViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions s_variablesJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Dispatcher _dispatcher;
    private readonly ILogger<TemplateTestViewModel> _logger;

    /// <summary>Initializes a new template test ViewModel.</summary>
    /// <param name="dispatcher">CQRS dispatcher.</param>
    /// <param name="logger">Logger instance.</param>
    public TemplateTestViewModel(Dispatcher dispatcher, ILogger<TemplateTestViewModel> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [ObservableProperty]
    private string? _templateId;

    [ObservableProperty]
    private string? _inlineTemplate;

    [ObservableProperty]
    private string _variablesJson = "{}";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private TemplateTestOutcome? _result;

    /// <summary>Builds sample JSON containing each required variable for a saved template.</summary>
    /// <param name="templateId">Template identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Indented JSON with required template variables.</returns>
    public async Task<string> BuildRequiredVariablesJsonAsync(string? templateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return "{}";

        try
        {
            var result = await _dispatcher
                .QueryAsync(new GetTemplateQuery(templateId.Trim()), ct)
                .ConfigureAwait(true);

            if (!result.IsSuccess || result.Value is null)
                return "{}";

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var variable in result.Value.Variables.Where(static variable => variable.Required))
            {
                if (string.IsNullOrWhiteSpace(variable.Name))
                    continue;

                variables[variable.Name] = CreateSampleValue(variable);
            }

            return variables.Count == 0
                ? "{}"
                : JsonSerializer.Serialize(variables, s_variablesJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return "{}";
        }
    }

    /// <summary>Builds sample JSON containing the supplied required variable names.</summary>
    /// <param name="variableNames">Required variable names.</param>
    /// <returns>Indented JSON with empty string values.</returns>
    public static string BuildVariablesJson(IEnumerable<string> variableNames)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variableName in variableNames)
        {
            if (!string.IsNullOrWhiteSpace(variableName))
                variables[variableName.Trim()] = string.Empty;
        }

        return variables.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(variables, s_variablesJsonOptions);
    }

    /// <summary>Executes a template test query with current input fields.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(TemplateId) && string.IsNullOrWhiteSpace(InlineTemplate))
        {
            Result = null;
            ErrorMessage = null;
            IsLoading = false;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        Result = null;

        try
        {
            var query = new TestTemplateQuery
            {
                TemplateId = string.IsNullOrWhiteSpace(TemplateId) ? null : TemplateId,
                InlineTemplate = string.IsNullOrWhiteSpace(InlineTemplate) ? null : InlineTemplate,
                VariablesJson = string.IsNullOrWhiteSpace(VariablesJson) ? "{}" : VariablesJson,
            };

            var dispatchResult = await _dispatcher.QueryAsync(query, ct).ConfigureAwait(true);
            if (!dispatchResult.IsSuccess)
            {
                ErrorMessage = dispatchResult.Error ?? "Unknown error running template test.";
                return;
            }

            Result = dispatchResult.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static object? CreateSampleValue(TemplateVariableDetail variable)
    {
        var candidate = !string.IsNullOrWhiteSpace(variable.DefaultValue)
            ? variable.DefaultValue
            : variable.Example;

        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        if (TryParseJsonValue(candidate, out var parsed))
            return parsed;

        return candidate;
    }

    private static bool TryParseJsonValue(string value, out JsonElement parsed)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            parsed = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            parsed = default;
            return false;
        }
    }
}
