using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Screens.Dependencies;

/// <summary>Loads dependency health from <see cref="DependencyHealthService"/> and maps it
/// to a flat list of rows with status tones and counts.</summary>
public sealed class DependencyViewModel : ObservableObject
{
    private readonly DependencyHealthService _engine;
    private readonly IWorkspaceContext _workspace;

    private DependencyView? _view;
    private bool _isLoading;
    private string? _errorMessage;

    public DependencyViewModel(DependencyHealthService engine, IWorkspaceContext workspace)
    {
        _engine = engine;
        _workspace = workspace;
    }

    public DependencyView? View { get => _view; private set => SetProperty(ref _view, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var report = _engine.Check(_workspace.UserDir, _workspace.ProjectDir);
            View = DependencyRowsMapper.Map(report);
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
}
