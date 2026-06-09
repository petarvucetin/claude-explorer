using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Compare;

/// <summary>
/// Applies a <see cref="CopyPlan"/> (produced by <see cref="ConfigCopyService"/>) through
/// <see cref="SafeMutationService"/>: preview → backup → write → change-log → undo.
///
/// <para>The <see cref="ResolvedTarget"/> scope is always <see cref="ScopeKind.Project"/> for
/// the target write — this is a reasonable default for change-log grouping when the copy crosses
/// endpoint boundaries; the actual file path determines what is written.</para>
///
/// <para><b>Move of non-JSON files (file-copy categories):</b> when
/// <c>plan.TargetIsJson == false</c> and <c>plan.SourceRemoval.NewContent == ""</c> (the delete
/// sentinel), a true file-move-delete is not yet supported through the undo-able mutator. The copy
/// is still applied; <see cref="Error"/> is set to a human-readable note, and the source is left
/// untouched.</para>
/// </summary>
public sealed class CopyViewModel
{
    private readonly SafeMutationService _svc;
    private readonly ConfigCopyService _copier;
    private readonly Func<string> _nowIso;

    /// <summary>The change-log entry for the most recently applied target write, or <c>null</c>
    /// if nothing has been applied yet.</summary>
    public ChangeLogEntry? Applied { get; private set; }

    /// <summary>A human-readable error from the last <see cref="Copy"/> / <see cref="Move"/> /
    /// <see cref="Undo"/> call, or <c>null</c> if the last operation succeeded.</summary>
    public string? Error { get; private set; }

    public CopyViewModel(SafeMutationService svc, ConfigCopyService copier, Func<string> nowIso)
    {
        _svc = svc;
        _copier = copier;
        _nowIso = nowIso;
    }

    // ── Public operations ────────────────────────────────────────────────────

    /// <summary>Copy the item described by <paramref name="req"/> to its target scope.
    /// Sets <see cref="Applied"/> and clears <see cref="Error"/> on success; sets
    /// <see cref="Error"/> and leaves <see cref="Applied"/> as-is on failure.</summary>
    public void Copy(CopyRequest req)
    {
        Error = null;
        try
        {
            var plan = _copier.PlanCopy(req);
            Applied = ApplyTarget(plan, req);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Move the item described by <paramref name="req"/>: copy to target then remove
    /// from source. For non-JSON file moves where the source removal is a delete (empty sentinel),
    /// the copy is still applied but the delete is skipped and <see cref="Error"/> is set.</summary>
    public void Move(CopyRequest req)
    {
        Error = null;
        try
        {
            var plan = _copier.PlanMove(req);
            Applied = ApplyTarget(plan, req);

            if (plan.SourceRemoval is { } removal)
            {
                // Special case: file delete not supported through the undo-able mutator.
                if (!plan.TargetIsJson && removal.NewContent == "")
                {
                    Error = "Move of files is not supported yet; copied without removing source.";
                    return;
                }

                // Apply the source removal as a second edit.
                var sourceTarget = new ResolvedTarget(ScopeKind.User, removal.Path);
                var sourceValidation = plan.TargetIsJson
                    ? new SettingsValidator().Validate(removal.NewContent)
                    : ValidationResult.Ok;
                var sourcePreview = _svc.PreviewEdit(sourceTarget, removal.NewContent, sourceValidation);
                _svc.ApplyEdit(sourcePreview, _nowIso(), $"Move {req.Category} {req.Key} (remove source)");
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Undo the last applied target write (via <see cref="SafeMutationService.Undo"/>).
    /// Does nothing if nothing has been applied. Sets <see cref="Error"/> on failure.</summary>
    public void Undo()
    {
        if (Applied is null) return;
        try
        {
            _svc.Undo(Applied);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ChangeLogEntry ApplyTarget(CopyPlan plan, CopyRequest req)
    {
        var target = new ResolvedTarget(ScopeKind.Project, plan.TargetPath);

        var validation = plan.TargetIsJson
            ? new SettingsValidator().Validate(plan.NewTargetContent)
            : ValidationResult.Ok;

        var preview = _svc.PreviewEdit(target, plan.NewTargetContent, validation);
        return _svc.ApplyEdit(preview, _nowIso(), $"Copy {req.Category} {req.Key}");
    }
}
