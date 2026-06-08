using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Screens.Hooks;

/// <summary>Loads the effective hooks for the active workspace (merged across scopes + plugins via
/// <see cref="EffectiveConfigService"/>), joins them with dependency health, and exposes grouped rows
/// + a selected row for the detail panel.</summary>
public sealed class HooksViewModel : ObservableObject
{
    private readonly EffectiveConfigService _config;
    private readonly DependencyHealthService _health;
    private readonly IWorkspaceContext _workspace;

    private HookView? _view;
    private HookRow? _selected;
    private bool _isLoading;
    private string? _errorMessage;

    public HooksViewModel(EffectiveConfigService config, DependencyHealthService health, IWorkspaceContext workspace)
    {
        _config = config;
        _health = health;
        _workspace = workspace;
    }

    public HookView? View { get => _view; private set => SetProperty(ref _view, value); }
    public HookRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var config = _config.Compute(_workspace.UserDir, _workspace.ProjectDir);
            var report = _health.Check(_workspace.UserDir, _workspace.ProjectDir);
            View = HookRowsMapper.Map(config, report);
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
