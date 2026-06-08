using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Screens.Marketplace;

/// <summary>
/// ViewModel for the Marketplace screen: Browse installed items, fetch an added source
/// (metadata-only, community trust), and install items via the <c>claude</c> CLI through
/// <see cref="SafeMutationService"/>.
/// </summary>
public sealed class MarketplaceViewModel : ObservableObject
{
    private readonly CatalogService _catalog;
    private readonly SafeMutationService _mutation;
    private readonly IWorkspaceContext _workspace;
    private readonly Func<string> _nowIso;
    private readonly DependencyHealthService? _depHealth;

    private IReadOnlyList<MarketplaceItemRow> _items = Array.Empty<MarketplaceItemRow>();
    private IReadOnlyList<MarketplaceItemRow> _addedItems = Array.Empty<MarketplaceItemRow>();
    private bool _isLoading;
    private string? _error;
    private ChangeLogEntry? _lastInstall;
    private string _addSourceInput = "";
    private bool _showTrustWarning;
    private ScopeKind _installScope = ScopeKind.User;
    private IReadOnlyList<string> _missingRuntimes = Array.Empty<string>();

    public MarketplaceViewModel(
        CatalogService catalog,
        SafeMutationService mutation,
        IWorkspaceContext workspace,
        Func<string> nowIso,
        DependencyHealthService? depHealth = null)
    {
        _catalog = catalog;
        _mutation = mutation;
        _workspace = workspace;
        _nowIso = nowIso;
        _depHealth = depHealth;
    }

    /// <summary>Items from installed marketplaces (Installed tab).</summary>
    public IReadOnlyList<MarketplaceItemRow> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    /// <summary>Items fetched from a user-added source (Add-source tab, metadata-only).</summary>
    public IReadOnlyList<MarketplaceItemRow> AddedItems
    {
        get => _addedItems;
        private set => SetProperty(ref _addedItems, value);
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public ChangeLogEntry? LastInstall { get => _lastInstall; private set => SetProperty(ref _lastInstall, value); }

    public string AddSourceInput
    {
        get => _addSourceInput;
        set => SetProperty(ref _addSourceInput, value);
    }

    /// <summary>True after a user-added source has been fetched — signals the community trust warning.</summary>
    public bool ShowTrustWarning
    {
        get => _showTrustWarning;
        private set => SetProperty(ref _showTrustWarning, value);
    }

    /// <summary>Scope to install items into. Defaults to User. Bound to the scope picker in the UI.</summary>
    public ScopeKind InstallScope
    {
        get => _installScope;
        set => SetProperty(ref _installScope, value);
    }

    /// <summary>
    /// Distinct names of runtimes that are currently missing on this machine (derived from the
    /// dependency health check run against the user + project config).
    /// Tech-debt: item-level runtime metadata is not yet available in catalog items; this is a
    /// machine-level check — any missing runtime may affect an installed item.
    /// </summary>
    public IReadOnlyList<string> MissingRuntimes
    {
        get => _missingRuntimes;
        private set => SetProperty(ref _missingRuntimes, value);
    }

    /// <summary>Load installed catalog items (no network) and run the machine-level dep health check.</summary>
    public void LoadInstalled()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var raw = _catalog.BuildInstalledCatalog(_workspace.UserDir);
            Items = MarketplaceMapper.Map(raw);

            // Compute missing runtimes from a machine-level dep health check.
            // Tech-debt: item-level runtime requirements are not yet in catalog metadata,
            // so this is a machine-wide check — any missing runtime could affect installed items.
            if (_depHealth is not null)
            {
                try
                {
                    var report = _depHealth.Check(_workspace.UserDir, _workspace.ProjectDir);
                    MissingRuntimes = report.Results
                        .Where(r => r.Status.Kind == DependencyStatusKind.Missing)
                        .Select(r => r.Ref.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch
                {
                    // Dep health is advisory — never crash the load.
                    MissingRuntimes = Array.Empty<string>();
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Fetch metadata from a user-added source (URL / GitHub / owner-repo). No download/install.</summary>
    public void AddSource(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        IsLoading = true;
        Error = null;
        AddedItems = Array.Empty<MarketplaceItemRow>();
        ShowTrustWarning = false;
        try
        {
            var raw = _catalog.FetchAddedSource(input);
            AddedItems = MarketplaceMapper.Map(raw);
            ShowTrustWarning = AddedItems.Count > 0;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Install an item via the <c>claude</c> CLI through the safe-mutation layer.
    /// Uses <see cref="InstallScope"/> if no explicit scope is provided.</summary>
    public void Install(MarketplaceItemRow item, ScopeKind? scope = null)
    {
        Error = null;
        try
        {
            var request = new InstallRequest(
                item.Name,
                scope ?? InstallScope,
                MarketplaceMapper.InstallArgs(item.Name),
                MarketplaceMapper.UninstallArgs(item.Name));
            LastInstall = _mutation.Install(request, _nowIso());
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Undo the most recent install.</summary>
    public void UndoLastInstall()
    {
        if (LastInstall is null) return;
        try
        {
            _mutation.Undo(LastInstall);
            LastInstall = LastInstall with { IsUndone = true };
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
