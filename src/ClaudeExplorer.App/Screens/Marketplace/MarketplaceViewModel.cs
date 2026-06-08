using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Catalog;
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

    private IReadOnlyList<MarketplaceItemRow> _items = Array.Empty<MarketplaceItemRow>();
    private IReadOnlyList<MarketplaceItemRow> _addedItems = Array.Empty<MarketplaceItemRow>();
    private bool _isLoading;
    private string? _error;
    private ChangeLogEntry? _lastInstall;
    private string _addSourceInput = "";
    private bool _showTrustWarning;

    public MarketplaceViewModel(
        CatalogService catalog,
        SafeMutationService mutation,
        IWorkspaceContext workspace,
        Func<string> nowIso)
    {
        _catalog = catalog;
        _mutation = mutation;
        _workspace = workspace;
        _nowIso = nowIso;
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

    /// <summary>Load installed catalog items (no network).</summary>
    public void LoadInstalled()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var raw = _catalog.BuildInstalledCatalog(_workspace.UserDir);
            Items = MarketplaceMapper.Map(raw);
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

    /// <summary>Install an item via the <c>claude</c> CLI through the safe-mutation layer.</summary>
    public void Install(MarketplaceItemRow item, ScopeKind scope = ScopeKind.User)
    {
        Error = null;
        try
        {
            var request = new InstallRequest(
                item.Name,
                scope,
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
