using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Screens.Recommendations;

/// <summary>
/// ViewModel for the Recommendations screen. Runs <see cref="RecommendationService.Recommend"/>
/// against the installed catalog and maps the result to view rows via <see cref="RecommendationsMapper"/>.
/// </summary>
public sealed class RecommendationsViewModel : ObservableObject
{
    private readonly CatalogService _catalog;
    private readonly RecommendationService _recommendations;
    private readonly IWorkspaceContext _workspace;

    private RecommendationsView? _view;
    private bool _isLoading;
    private string? _errorMessage;

    public RecommendationsViewModel(
        CatalogService catalog,
        RecommendationService recommendations,
        IWorkspaceContext workspace)
    {
        _catalog = catalog;
        _recommendations = recommendations;
        _workspace = workspace;
    }

    public RecommendationsView? View { get => _view; private set => SetProperty(ref _view, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var catalog = _catalog.BuildInstalledCatalog(_workspace.UserDir);
            var result = _recommendations.Recommend(_workspace.UserDir, _workspace.ProjectDir, catalog);
            View = RecommendationsMapper.Map(result);
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
