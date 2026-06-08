using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Screens.ChangeLog;

/// <summary>Reads the shared <see cref="SafeMutationService.ChangeLog"/> (singleton) and exposes
/// the entries grouped by scope. Provides an <see cref="Undo"/> command that delegates to the
/// service and refreshes the view.</summary>
public sealed class ChangeLogViewModel : ObservableObject
{
    private readonly SafeMutationService _svc;

    private IReadOnlyList<IGrouping<ScopeKind, ChangeLogEntry>> _groups =
        Array.Empty<IGrouping<ScopeKind, ChangeLogEntry>>();
    private string? _errorMessage;

    public ChangeLogViewModel(SafeMutationService svc)
    {
        _svc = svc;
    }

    public IReadOnlyList<IGrouping<ScopeKind, ChangeLogEntry>> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        Groups = _svc.ChangeLog.ByScope();
        ErrorMessage = null;
    }

    public void Undo(ChangeLogEntry entry)
    {
        try
        {
            _svc.Undo(entry);
            Load(); // re-read so IsUndone reflects reality
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
