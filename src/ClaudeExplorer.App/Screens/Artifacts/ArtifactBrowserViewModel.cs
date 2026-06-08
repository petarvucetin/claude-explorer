using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.App.Screens.Artifacts;

/// <summary>ViewModel for the Commands &amp; Skills master/detail browser.
/// Loads an <see cref="ArtifactCatalog"/> from <see cref="ArtifactCatalogService"/>, groups by
/// source, and exposes filter/search + a selected item for the detail panel.</summary>
public sealed class ArtifactBrowserViewModel : ObservableObject
{
    private readonly ArtifactCatalogService _engine;
    private readonly IWorkspaceContext _workspace;

    private IReadOnlyList<ArtifactGroup> _allGroups = Array.Empty<ArtifactGroup>();
    private IReadOnlyList<ArtifactGroup> _filteredGroups = Array.Empty<ArtifactGroup>();
    private ArtifactItem? _selected;
    private ArtifactKind? _kindFilter;
    private string _search = "";
    private bool _isLoading;
    private string? _errorMessage;

    public ArtifactBrowserViewModel(ArtifactCatalogService engine, IWorkspaceContext workspace)
    {
        _engine = engine;
        _workspace = workspace;
    }

    public IReadOnlyList<ArtifactGroup> Groups { get => _filteredGroups; private set => SetProperty(ref _filteredGroups, value); }
    public ArtifactItem? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public ArtifactKind? KindFilter
    {
        get => _kindFilter;
        set { SetProperty(ref _kindFilter, value); ApplyFilter(); }
    }

    public string Search
    {
        get => _search;
        set { SetProperty(ref _search, value); ApplyFilter(); }
    }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var catalog = _engine.Build(_workspace.UserDir, _workspace.ProjectDir);
            _allGroups = ArtifactBrowserMapper.Group(catalog);
            ApplyFilter();
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Groups = ArtifactBrowserMapper.Filter(_allGroups, _kindFilter, _search);
    }
}
