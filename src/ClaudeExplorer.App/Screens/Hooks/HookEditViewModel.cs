using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Screens.Hooks;

/// <summary>
/// Drives the inline edit of a single hook's matcher-group. Loads the block from the source file,
/// previews/saves by splicing it back into the whole file and routing through
/// <see cref="SafeMutationService"/> (diff → backup → validate → change-log → undo). Read-only when the
/// row's source is plugin/enterprise. Mirrors <c>SafeEditViewModel</c>; <paramref name="nowIso"/> is the
/// injectable clock seam.
/// </summary>
public sealed class HookEditViewModel : ObservableObject
{
    private readonly SafeMutationService _svc;
    private readonly IFileSystem _fs;
    private readonly HookRow _row;
    private readonly Func<string> _nowIso;
    private readonly string _projectDir;

    private string _blockText = "";
    private EditPreview? _preview;
    private ChangeLogEntry? _applied;
    private string? _error;

    public HookEditViewModel(SafeMutationService svc, IFileSystem fs, HookRow row, Func<string> nowIso, string projectDir)
    {
        _svc = svc;
        _fs = fs;
        _row = row;
        _nowIso = nowIso;
        _projectDir = projectDir;
        Load();
    }

    public HookRow Row => _row;
    public bool IsEditable => _row.IsEditable;

    /// <summary>True when the editable source is not project-specific (User/Enterprise/Plugin) — the
    /// "affects every project" warning.</summary>
    public bool IsGlobalEdit => _row.Source is not (ScopeKind.Project or ScopeKind.Local);

    public string BlockText { get => _blockText; set => SetProperty(ref _blockText, value); }
    public EditPreview? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public ChangeLogEntry? Applied { get => _applied; private set => SetProperty(ref _applied, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    private void Load()
    {
        try { BlockText = HookBlockEditor.ExtractBlock(ReadSource(), _row.Event, _row.SourceGroupIndex); }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void DoPreview()
    {
        Error = null;
        try
        {
            var newWhole = HookBlockEditor.SpliceBlock(ReadSource(), _row.Event, _row.SourceGroupIndex, BlockText);
            var winner = new SettingOrigin(_row.Source, _row.SourceFile, $"hooks.{_row.Event}");
            Preview = _svc.PreviewSettingsEdit(EditMode.EditWinner, _projectDir, winner, newWhole);
        }
        catch (Exception ex) { Error = ex.Message; Preview = null; }
    }

    public void Save()
    {
        if (!IsEditable) { Error = "This hook is read-only (plugin/managed source)."; return; }
        if (Preview is null) DoPreview();
        if (Preview is null) return;
        if (!Preview.Validation.IsValid) { Error = string.Join("; ", Preview.Validation.Errors); return; }
        try
        {
            Applied = _svc.ApplyEdit(Preview, _nowIso(), $"Edit {_row.Event} hook ({Summarize(_row.Matcher)})");
            Error = null;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void Undo()
    {
        if (Applied is null) return;
        try { _svc.Undo(Applied); Applied = Applied with { IsUndone = true }; Error = null; }
        catch (Exception ex) { Error = ex.Message; }
    }

    private string ReadSource() => _fs.FileExists(_row.SourceFile) ? _fs.ReadAllText(_row.SourceFile) : "";

    private static string Summarize(string matcher) => matcher.Length <= 24 ? matcher : matcher[..24] + "…";
}
