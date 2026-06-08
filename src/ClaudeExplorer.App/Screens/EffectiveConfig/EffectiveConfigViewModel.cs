using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Mutation;
using IWorkspaceContext = ClaudeExplorer.App.Services.IWorkspaceContext;

namespace ClaudeExplorer.App.Screens.EffectiveConfig;

/// <summary>Loads the effective config via <see cref="EffectiveConfigService"/>, maps it to a
/// view-ready model, and exposes it for the precedence matrix view. Also holds the
/// <see cref="SafeMutationService"/> reference so the safe-edit panel can use it.</summary>
public sealed class EffectiveConfigViewModel : ObservableObject
{
    private readonly EffectiveConfigService _engine;
    private readonly IWorkspaceContext _workspace;

    private EffectiveConfigView? _view;
    private bool _isLoading;
    private string? _errorMessage;

    public EffectiveConfigViewModel(
        EffectiveConfigService engine,
        IWorkspaceContext workspace,
        SafeMutationService mutations)
    {
        _engine = engine;
        _workspace = workspace;
        Mutations = mutations;
    }

    public EffectiveConfigView? View { get => _view; private set => SetProperty(ref _view, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    /// <summary>Shared mutations service; the safe-edit panel uses this directly.</summary>
    public SafeMutationService Mutations { get; }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var config = _engine.Compute(_workspace.UserDir, _workspace.ProjectDir);
            View = EffectiveConfigMapper.Map(config);
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
