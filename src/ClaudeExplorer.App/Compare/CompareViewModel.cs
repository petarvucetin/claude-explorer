using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.Compare;

/// <summary>Drives the Compare screen: pick left/right environments, snapshot both, diff, expose the
/// comparison + the selected category. View binds to <see cref="Comparison"/> / <see cref="LeftEnv"/> /
/// <see cref="RightEnv"/> / <see cref="SelectedCategory"/>.</summary>
public sealed class CompareViewModel : ObservableObject
{
    private readonly EnvironmentService _environments;
    private readonly IEnvironmentCompareDataSource _source;

    private ClaudeEnvironment? _left;
    private ClaudeEnvironment? _right;
    private EnvironmentComparison? _comparison;
    private CompareCategory? _selected;
    private bool _isLoading;
    private string? _error;

    public CompareViewModel(EnvironmentService environments, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _source = source;
    }

    public IReadOnlyList<ClaudeEnvironment> Environments => _environments.Environments;
    public ClaudeEnvironment? LeftEnv { get => _left; private set => SetProperty(ref _left, value); }
    public ClaudeEnvironment? RightEnv { get => _right; private set => SetProperty(ref _right, value); }
    public EnvironmentComparison? Comparison { get => _comparison; private set => SetProperty(ref _comparison, value); }
    public CompareCategory? SelectedCategory { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _error; private set => SetProperty(ref _error, value); }

    public void SetEnvironments(string leftId, string rightId)
    {
        LeftEnv = _environments.Environments.FirstOrDefault(e => e.Id == leftId);
        RightEnv = _environments.Environments.FirstOrDefault(e => e.Id == rightId);
        Load();
    }

    public void SelectCategory(string name)
        => SelectedCategory = Comparison?.Find(name) ?? SelectedCategory;

    public void Load()
    {
        IsLoading = true;
        try
        {
            var envs = _environments.Environments;
            LeftEnv ??= envs.FirstOrDefault();
            RightEnv ??= envs.Skip(1).FirstOrDefault() ?? LeftEnv;
            if (LeftEnv is null || RightEnv is null)
            {
                ErrorMessage = "Need two environments to compare.";
                Comparison = null;
                return;
            }
            ErrorMessage = null;
            Comparison = EnvironmentComparer.Compare(_source.Snapshot(LeftEnv), _source.Snapshot(RightEnv));
            SelectedCategory = Comparison.Categories.FirstOrDefault();
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
}
