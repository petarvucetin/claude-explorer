using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Compare;

/// <summary>
/// Applies a <see cref="CopyPlan"/> (from <see cref="ConfigCopyService"/>) through
/// <see cref="SafeMutationService"/>: each target write is preview → backup → write → change-log;
/// each Move removal is either a JSON content edit (settings/MCP/hooks) or a real undo-able delete
/// (files/dirs). A whole copy/move is applied as ONE logical group: <see cref="Undo"/> reverts every
/// recorded entry (writes + removals) so a recursive folder copy reverts atomically from the user's
/// perspective. The target scope is recorded as <see cref="ScopeKind.Project"/> (a reasonable
/// change-log grouping for cross-endpoint copies); the file path determines what is written.
/// </summary>
public sealed class CopyViewModel
{
    private readonly SafeMutationService _svc;
    private readonly ConfigCopyService _copier;
    private readonly Func<string> _nowIso;

    private readonly List<ChangeLogEntry> _applied = new();

    /// <summary>The change-log entry for the last applied target write (the first write of the plan),
    /// or null when nothing has been applied.</summary>
    public ChangeLogEntry? Applied => _applied.Count > 0 ? _applied[0] : null;

    /// <summary>A human-readable error from the last operation, or null on success.</summary>
    public string? Error { get; private set; }

    public CopyViewModel(SafeMutationService svc, ConfigCopyService copier, Func<string> nowIso)
    {
        _svc = svc;
        _copier = copier;
        _nowIso = nowIso;
    }

    public void Copy(CopyRequest req) => Run(() => _copier.PlanCopy(req), req);

    public void Move(CopyRequest req) => Run(() => _copier.PlanMove(req), req);

    private void Run(Func<CopyPlan> plan, CopyRequest req)
    {
        Error = null;
        _applied.Clear();
        try
        {
            var p = plan();
            // Writes first.
            foreach (var w in p.Writes)
            {
                var target = new ResolvedTarget(ScopeKind.Project, w.Path);
                var validation = w.IsJson ? new SettingsValidator().Validate(w.Content) : ValidationResult.Ok;
                var preview = _svc.PreviewEdit(target, w.Content, validation);
                _applied.Add(_svc.ApplyEdit(preview, _nowIso(), $"Copy {req.Category} {req.Key}"));
            }
            // Then source removals (delete files/dirs, or splice JSON).
            foreach (var r in p.Removals)
            {
                if (r.IsDelete)
                {
                    _applied.Add(_svc.ApplyDelete(
                        new ResolvedTarget(ScopeKind.User, r.Path), _nowIso(),
                        $"Move {req.Category} {req.Key} (remove source)"));
                }
                else
                {
                    var srcTarget = new ResolvedTarget(ScopeKind.User, r.Path);
                    var validation = new SettingsValidator().Validate(r.NewContent);
                    var preview = _svc.PreviewEdit(srcTarget, r.NewContent, validation);
                    _applied.Add(_svc.ApplyEdit(preview, _nowIso(), $"Move {req.Category} {req.Key} (remove source)"));
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Undo the whole group (every write + removal), in reverse order so re-creations and
    /// restores apply cleanly.</summary>
    public void Undo()
    {
        if (_applied.Count == 0) return;
        try
        {
            for (int i = _applied.Count - 1; i >= 0; i--)
                _svc.Undo(_applied[i]);
            _applied.Clear();
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
