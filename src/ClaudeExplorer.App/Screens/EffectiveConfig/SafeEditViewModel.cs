using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Screens.EffectiveConfig;

/// <summary>
/// Drives the 3-step safe-edit flow for a single setting:
///   1. Compose — pick <see cref="Mode"/> and write <see cref="NewContent"/>
///   2. Preview  — calls <see cref="SafeMutationService.PreviewSettingsEdit"/> → shows diff + validation
///   3. Apply    — calls <see cref="SafeMutationService.ApplyEdit"/> → shows applied banner with Undo
///
/// Inject a clock seam (<paramref name="nowIso"/>) so the timestamp is deterministic in tests.
/// </summary>
public sealed class SafeEditViewModel : ObservableObject
{
    private readonly SafeMutationService _svc;
    private readonly SettingOrigin? _winner;
    private readonly Func<string> _nowIso;
    private readonly string _projectDir;

    private EditMode _mode = EditMode.EditWinner;
    private string _newContent = "";
    private EditPreview? _preview;
    private ChangeLogEntry? _applied;
    private string? _error;

    public SafeEditViewModel(SafeMutationService svc, SettingOrigin? winner, Func<string> nowIso, string projectDir = "/project")
    {
        _svc = svc;
        _winner = winner;
        _nowIso = nowIso;
        _projectDir = projectDir;
    }

    public EditMode Mode { get => _mode; set => SetProperty(ref _mode, value); }
    public string NewContent { get => _newContent; set => SetProperty(ref _newContent, value); }
    public EditPreview? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public ChangeLogEntry? Applied { get => _applied; private set => SetProperty(ref _applied, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    /// <summary>True when editing the winning source, which may affect all projects using that scope.
    /// Any winner that is not project-specific (Project/Local) is treated as global — this covers
    /// User, Enterprise, and the Plugin base layer, and stays correct if more scopes are added.</summary>
    public bool IsGlobalEdit => Mode == EditMode.EditWinner &&
        (_winner is null || _winner.Scope is not (ScopeKind.Project or ScopeKind.Local));

    public void DoPreview()
    {
        Error = null;
        try
        {
            Preview = _svc.PreviewSettingsEdit(Mode, _projectDir, _winner, NewContent);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public void Apply()
    {
        if (Preview is null) { Error = "Preview first."; return; }
        if (!Preview.Validation.IsValid) { Error = string.Join("; ", Preview.Validation.Errors); return; }
        try
        {
            Applied = _svc.ApplyEdit(Preview, _nowIso());
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public void Undo()
    {
        if (Applied is null) return;
        try
        {
            _svc.Undo(Applied);
            Applied = Applied with { IsUndone = true };
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public void Reset()
    {
        Preview = null;
        Applied = null;
        Error = null;
    }
}
