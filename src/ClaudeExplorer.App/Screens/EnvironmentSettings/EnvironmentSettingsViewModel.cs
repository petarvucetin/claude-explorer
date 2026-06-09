using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core;

namespace ClaudeExplorer.App.Screens.EnvironmentSettings;

/// <summary>Loads the effective config for the active environment via
/// <see cref="EffectiveConfigService"/>, maps it to <see cref="EnvironmentSettingsView"/>,
/// and exposes it to the Claude Environment Settings page.</summary>
public sealed class EnvironmentSettingsViewModel : ObservableObject
{
    private readonly EffectiveConfigService _engine;
    private readonly IWorkspaceContext _workspace;
    private readonly EnvironmentService _envService;

    private EnvironmentSettingsView? _view;
    private bool _isLoading;
    private string? _errorMessage;

    public EnvironmentSettingsViewModel(
        EffectiveConfigService engine,
        IWorkspaceContext workspace,
        EnvironmentService envService)
    {
        _engine = engine;
        _workspace = workspace;
        _envService = envService;
    }

    public EnvironmentSettingsView? View { get => _view; private set => SetProperty(ref _view, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var active = _envService.Active;
            var identity = new EnvironmentIdentity(
                active.Name,
                active.Kind,
                active.UserDir,
                _workspace.ProjectLabel);

            var config = _engine.Compute(_workspace.UserDir, _workspace.ProjectDir);
            View = EnvironmentSettingsMapper.Map(config, identity);
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
