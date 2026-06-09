using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.Compare;

/// <summary>Drives the Compare screen: pick left/right endpoints (bases from environments +
/// projects from the registry), snapshot both, diff, expose the comparison + the selected
/// category. View binds to <see cref="Comparison"/> / <see cref="LeftEndpoint"/> /
/// <see cref="RightEndpoint"/> / <see cref="SelectedCategory"/>.</summary>
public sealed class CompareViewModel : ObservableObject
{
    private readonly EnvironmentService _environments;
    private readonly ProjectRegistry _projects;
    private readonly IEnvironmentCompareDataSource _source;

    private CompareEndpoint? _left;
    private CompareEndpoint? _right;
    private EnvironmentComparison? _comparison;
    private CompareCategory? _selected;
    private bool _isLoading;
    private string? _error;

    public CompareViewModel(EnvironmentService environments, ProjectRegistry projects, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _projects = projects;
        _source = source;
    }

    public IReadOnlyList<CompareEndpoint> Endpoints =>
        _environments.Environments.Select(e => CompareEndpoint.Base(e.Id, e.Name, e.UserDir))
            .Concat(_projects.All.Select(p => CompareEndpoint.Project(p.Id, p.Name, p.ProjectDir)))
            .ToList();

    public CompareEndpoint? LeftEndpoint { get => _left; private set => SetProperty(ref _left, value); }
    public CompareEndpoint? RightEndpoint { get => _right; private set => SetProperty(ref _right, value); }
    public EnvironmentComparison? Comparison { get => _comparison; private set => SetProperty(ref _comparison, value); }
    public CompareCategory? SelectedCategory { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _error; private set => SetProperty(ref _error, value); }

    public void SetEndpoints(string leftId, string rightId)
    {
        var eps = Endpoints;
        LeftEndpoint = eps.FirstOrDefault(e => e.Id == leftId);
        RightEndpoint = eps.FirstOrDefault(e => e.Id == rightId);
        Load();
    }

    public void Swap() { (LeftEndpoint, RightEndpoint) = (RightEndpoint, LeftEndpoint); Load(); }

    public void SelectCategory(string name) => SelectedCategory = Comparison?.Find(name) ?? SelectedCategory;

    public void Load()
    {
        IsLoading = true;
        try
        {
            var eps = Endpoints;
            LeftEndpoint ??= eps.FirstOrDefault();
            RightEndpoint ??= eps.Skip(1).FirstOrDefault() ?? LeftEndpoint;
            if (LeftEndpoint is null || RightEndpoint is null)
            {
                ErrorMessage = "Add at least two endpoints (a base + a project) to compare.";
                Comparison = null;
                return;
            }
            ErrorMessage = null;
            Comparison = EnvironmentComparer.Compare(_source.Snapshot(LeftEndpoint), _source.Snapshot(RightEndpoint));
            SelectedCategory = Comparison.Categories.FirstOrDefault();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }
}
