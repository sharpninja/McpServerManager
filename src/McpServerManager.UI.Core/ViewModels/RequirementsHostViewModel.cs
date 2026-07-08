using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// PLAN-REQSDESKTOP-001 / FR-REQS-DESKTOP-001: host ViewModel that composes the requirements list,
/// detail, mapping, and generate ViewModels into a single Desktop surface, and adds push-to-wiki and
/// crosslink navigation. State + dispatch only (logic lives in the reused VMs and CQRS handlers).
/// </summary>
public sealed partial class RequirementsHostViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<RequirementsHostViewModel> _logger;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private string? _currentRequirementId;

    /// <summary>Initializes the requirements host with its reused child ViewModels.</summary>
    /// <param name="dispatcher">CQRS dispatcher.</param>
    /// <param name="functional">Functional requirement list VM.</param>
    /// <param name="technical">Technical requirement list VM.</param>
    /// <param name="testing">Testing requirement list VM.</param>
    /// <param name="mapping">FR-to-TR mapping list VM.</param>
    /// <param name="functionalDetail">Functional requirement detail VM (crosslink view).</param>
    /// <param name="generate">Requirements document generation VM.</param>
    /// <param name="logger">Logger.</param>
    public RequirementsHostViewModel(
        Dispatcher dispatcher,
        FrListViewModel functional,
        TrListViewModel technical,
        TestListViewModel testing,
        MappingListViewModel mapping,
        FrDetailViewModel functionalDetail,
        RequirementsGenerateViewModel generate,
        ILogger<RequirementsHostViewModel>? logger = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Functional = functional ?? throw new ArgumentNullException(nameof(functional));
        Technical = technical ?? throw new ArgumentNullException(nameof(technical));
        Testing = testing ?? throw new ArgumentNullException(nameof(testing));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        FunctionalDetail = functionalDetail ?? throw new ArgumentNullException(nameof(functionalDetail));
        Generate = generate ?? throw new ArgumentNullException(nameof(generate));
        _logger = logger ?? NullLogger<RequirementsHostViewModel>.Instance;
    }

    /// <summary>Functional requirement list VM.</summary>
    public FrListViewModel Functional { get; }

    /// <summary>Technical requirement list VM.</summary>
    public TrListViewModel Technical { get; }

    /// <summary>Testing requirement list VM.</summary>
    public TestListViewModel Testing { get; }

    /// <summary>FR-to-TR mapping list VM.</summary>
    public MappingListViewModel Mapping { get; }

    /// <summary>Functional requirement detail VM (drives the crosslink view).</summary>
    public FrDetailViewModel FunctionalDetail { get; }

    /// <summary>Requirements document generation VM.</summary>
    public RequirementsGenerateViewModel Generate { get; }

    /// <summary>Back/forward navigation over visited requirement ids (crosslink view).</summary>
    public NavigationStackService<string> RequirementNavigation { get; } = new();

    /// <summary>Whether the crosslink view can navigate back.</summary>
    public bool CanNavigateBack => RequirementNavigation.CanGoBack;

    /// <summary>Whether the crosslink view can navigate forward.</summary>
    public bool CanNavigateForward => RequirementNavigation.CanGoForward;

    /// <summary>Loads all requirement tabs.</summary>
    [RelayCommand]
    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = "Loading requirements...";
        try
        {
            await Functional.LoadAsync(ct).ConfigureAwait(true);
            await Technical.LoadAsync(ct).ConfigureAwait(true);
            await Testing.LoadAsync(ct).ConfigureAwait(true);
            await Mapping.LoadAsync(ct).ConfigureAwait(true);
            StatusMessage = "Requirements loaded.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load requirements");
            StatusMessage = "Load failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Generates the requirements document via the generate VM.</summary>
    [RelayCommand]
    public async Task GenerateDocumentAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            await Generate.GenerateAsync(ct: ct).ConfigureAwait(true);
            StatusMessage = Generate.StatusMessage ?? StatusMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Generates the requirements wiki and pushes it to GitHub.</summary>
    [RelayCommand]
    public Task PushToGitHubAsync(CancellationToken ct = default) => PushAsync(WikiPushTarget.GitHub, ct);

    /// <summary>Generates the requirements wiki and pushes it to Azure DevOps.</summary>
    [RelayCommand]
    public Task PushToAzureAsync(CancellationToken ct = default) => PushAsync(WikiPushTarget.Azure, ct);

    private async Task PushAsync(WikiPushTarget target, CancellationToken ct)
    {
        IsBusy = true;
        StatusMessage = $"Pushing requirements wiki to {target}...";
        try
        {
            var result = await _dispatcher
                .SendAsync(new PushRequirementsWikiCommand(target, Generate.DocSelector), ct)
                .ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                StatusMessage = $"Push to {target} failed: {result.Error}";
                return;
            }

            var push = result.Value!;
            StatusMessage = push.Success
                ? $"Pushed to {target}{(push.Location is null ? "" : $": {push.Location}")}"
                : $"Push to {target} failed: {push.Error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push to {Target} failed", target);
            StatusMessage = $"Push to {target} failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Imports a parsed set of requirements, creating each record.</summary>
    /// <param name="request">The parsed requirements to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The import result, or <see langword="null"/> on failure.</returns>
    public async Task<RequirementsImportResult?> ImportAsync(RequirementsImportRequest request, CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = "Importing requirements...";
        try
        {
            var result = await _dispatcher.SendAsync(new ImportRequirementsCommand(request), ct).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                StatusMessage = "Import failed: " + result.Error;
                return null;
            }

            var import = result.Value!;
            StatusMessage = $"Imported FR:{import.FunctionalCreated} TR:{import.TechnicalCreated} TEST:{import.TestingCreated}" +
                            (import.Errors.Count > 0 ? $" ({import.Errors.Count} error(s))" : "");
            await LoadAllAsync(ct).ConfigureAwait(true);
            return import;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            StatusMessage = "Import failed: " + ex.Message;
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Navigates the crosslink view to a requirement id, pushing onto the nav stack.</summary>
    /// <param name="id">The requirement id to open.</param>
    [RelayCommand]
    public async Task NavigateToRequirementAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        RequirementNavigation.Navigate(id.Trim());
        await LoadCurrentRequirementAsync().ConfigureAwait(true);
    }

    /// <summary>Navigates the crosslink view back.</summary>
    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        RequirementNavigation.Back();
        await LoadCurrentRequirementAsync().ConfigureAwait(true);
    }

    /// <summary>Navigates the crosslink view forward.</summary>
    [RelayCommand]
    public async Task NavigateForwardAsync()
    {
        RequirementNavigation.Forward();
        await LoadCurrentRequirementAsync().ConfigureAwait(true);
    }

    private async Task LoadCurrentRequirementAsync()
    {
        CurrentRequirementId = RequirementNavigation.Current;
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
        if (!string.IsNullOrWhiteSpace(CurrentRequirementId))
            await FunctionalDetail.LoadAsync(CurrentRequirementId).ConfigureAwait(true);
    }
}
