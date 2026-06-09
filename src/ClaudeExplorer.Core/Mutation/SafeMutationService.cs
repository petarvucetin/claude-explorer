using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Façade over the safe-mutation layer: resolves where an edit lands (<see cref="ScopeTargetResolver"/>),
/// previews it (diff + validation), applies it with backup + change-log, and supports install /
/// undo. One instance owns the session's <see cref="ChangeLog"/>. This is the single entry point
/// the UI (Phases 7–8) binds to.
/// </summary>
public sealed class SafeMutationService
{
    private readonly ScopeTargetResolver _resolver = new();
    private readonly Mutator _mutator;

    public ChangeLog ChangeLog { get; }

    public SafeMutationService(IFileSystem fs, IFileWriter writer, IBackupStore backups, IProcessRunner runner)
    {
        ChangeLog = new ChangeLog();
        _mutator = new Mutator(fs, writer, backups, ChangeLog, runner);
    }

    public ResolvedTarget ResolveTarget(EditMode mode, string projectDir, SettingOrigin? winner)
        => _resolver.Resolve(mode, projectDir, winner);

    public EditPreview PreviewSettingsEdit(EditMode mode, string projectDir, SettingOrigin? winner, string newContent)
        => _mutator.PreviewSettingsEdit(_resolver.Resolve(mode, projectDir, winner), newContent);

    /// <summary>Build a preview for an arbitrary file edit with an explicit validation result.
    /// Used by the App layer to apply copy/move plans from <c>ConfigCopyService</c>.</summary>
    public EditPreview PreviewEdit(ResolvedTarget target, string newContent, ValidationResult validation)
        => _mutator.PreviewEdit(target, newContent, validation);

    public ChangeLogEntry ApplyEdit(EditPreview preview, string timestamp, string? description = null)
        => _mutator.ApplyEdit(preview, timestamp, description);

    /// <summary>Safely delete a file (backup → delete → change-log record). Reversible via
    /// <see cref="Undo"/>, which re-creates the original content.</summary>
    public ChangeLogEntry ApplyDelete(ResolvedTarget target, string timestamp, string? description = null)
        => _mutator.ApplyDelete(target, timestamp, description);

    public ChangeLogEntry Install(InstallRequest request, string timestamp)
        => _mutator.Install(request, timestamp);

    public void Undo(ChangeLogEntry entry) => _mutator.Undo(entry);
}
