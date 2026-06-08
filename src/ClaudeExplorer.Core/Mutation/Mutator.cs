using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>A previewed config edit: the resolved destination, the before/after content, the diff,
/// and the validation outcome. <see cref="Mutator.ApplyEdit"/> refuses to write unless
/// <see cref="Validation"/> is valid.</summary>
public sealed record EditPreview(
    ResolvedTarget Target,
    string OldContent,
    string NewContent,
    Diff Diff,
    ValidationResult Validation,
    bool TargetExisted);

/// <summary>A request to install a catalog item by delegating to the <c>claude</c> CLI. The
/// uninstall args are captured up front so the change can be undone later.</summary>
public sealed record InstallRequest(
    string ItemName,
    ScopeKind Scope,
    IReadOnlyList<string> InstallArgs,
    IReadOnlyList<string> UninstallArgs);

/// <summary>Raised when a mutation is refused (invalid content) or fails (CLI non-zero exit).</summary>
public sealed class MutationException : Exception
{
    public MutationException(string message) : base(message) { }
}

/// <summary>
/// The single safe-mutation entry point. Config edits are direct file writes guarded by
/// validate → backup → write → record; installs delegate to the <c>claude</c> CLI via
/// <see cref="IProcessRunner"/>. Every applied change is reversible via <see cref="Undo"/>, which
/// restores (or deletes) the backed-up file for edits, or runs the recorded uninstall command for
/// installs.
/// </summary>
public sealed class Mutator
{
    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly IBackupStore _backups;
    private readonly ChangeLog _log;
    private readonly SettingsValidator _settingsValidator;
    private readonly DiffGenerator _diff;
    private readonly IProcessRunner _runner;
    private readonly string _claudeExecutable;

    public Mutator(
        IFileSystem fs,
        IFileWriter writer,
        IBackupStore backups,
        ChangeLog log,
        IProcessRunner runner,
        SettingsValidator? settingsValidator = null,
        string claudeExecutable = "claude")
    {
        _fs = fs;
        _writer = writer;
        _backups = backups;
        _log = log;
        _runner = runner;
        _settingsValidator = settingsValidator ?? new SettingsValidator();
        _diff = new DiffGenerator();
        _claudeExecutable = claudeExecutable;
    }

    /// <summary>Build a preview for replacing <paramref name="target"/>'s content with an explicit
    /// validation result. No write happens.</summary>
    public EditPreview PreviewEdit(ResolvedTarget target, string newContent, ValidationResult validation)
    {
        var existed = _fs.FileExists(target.FilePath);
        var oldContent = existed ? _fs.ReadAllText(target.FilePath) : "";
        return new EditPreview(target, oldContent, newContent, _diff.Generate(oldContent, newContent), validation, existed);
    }

    /// <summary>Preview a settings.json edit, validating the new content with the built-in
    /// <see cref="SettingsValidator"/>.</summary>
    public EditPreview PreviewSettingsEdit(ResolvedTarget target, string newContent)
        => PreviewEdit(target, newContent, _settingsValidator.Validate(newContent));

    /// <summary>Apply a previewed edit: refuse if invalid, back up the current file, write the new
    /// content, and record a reversible change-log entry.</summary>
    public ChangeLogEntry ApplyEdit(EditPreview preview, string timestamp, string? description = null)
    {
        if (!preview.Validation.IsValid)
            throw new MutationException(
                "Refusing to write invalid content: " + string.Join("; ", preview.Validation.Errors));

        var backup = _backups.Backup(
            preview.Target.FilePath,
            preview.TargetExisted ? preview.OldContent : null,
            preview.TargetExisted,
            timestamp);

        _writer.WriteAllText(preview.Target.FilePath, preview.NewContent);

        return _log.Record(new ChangeLogEntry(
            Id: "",
            Timestamp: timestamp,
            Kind: ChangeKind.Edit,
            Scope: preview.Target.Scope,
            FilePath: preview.Target.FilePath,
            Description: description ?? $"Edit {preview.Target.FilePath}",
            Backup: backup,
            UndoCommand: null,
            IsUndone: false));
    }

    /// <summary>Install a catalog item by running the <c>claude</c> CLI. Throws on non-zero exit;
    /// records an install entry carrying the uninstall command for undo.</summary>
    public ChangeLogEntry Install(InstallRequest request, string timestamp)
    {
        var result = _runner.Run(_claudeExecutable, request.InstallArgs);
        if (!result.Success)
            throw new MutationException(
                $"Install of '{request.ItemName}' failed (exit {result.ExitCode}): {result.StdErr}");

        return _log.Record(new ChangeLogEntry(
            Id: "",
            Timestamp: timestamp,
            Kind: ChangeKind.Install,
            Scope: request.Scope,
            FilePath: request.ItemName,
            Description: $"Install {request.ItemName}",
            Backup: null,
            UndoCommand: request.UninstallArgs,
            IsUndone: false));
    }

    /// <summary>Reverse a previously-applied change: restore (or delete) the file for an edit, or
    /// run the recorded uninstall command for an install. Marks the entry undone in the change log.</summary>
    public void Undo(ChangeLogEntry entry)
    {
        // Look up the current state in the log (the passed-in entry may be stale since records are immutable).
        var current = _log.Entries.FirstOrDefault(e => e.Id == entry.Id) ?? entry;
        if (current.IsUndone)
            throw new MutationException($"Change '{entry.Id}' has already been undone.");

        switch (entry.Kind)
        {
            case ChangeKind.Edit:
                if (entry.Backup is null)
                    throw new MutationException($"Change '{entry.Id}' has no backup to restore.");
                if (entry.Backup.OriginalExisted)
                    _writer.WriteAllText(entry.Backup.OriginalPath, _backups.Read(entry.Backup));
                else
                    _writer.Delete(entry.Backup.OriginalPath);
                break;

            case ChangeKind.Install:
                if (entry.UndoCommand is null)
                    throw new MutationException($"Change '{entry.Id}' has no uninstall command.");
                var result = _runner.Run(_claudeExecutable, entry.UndoCommand);
                if (!result.Success)
                    throw new MutationException($"Uninstall failed (exit {result.ExitCode}): {result.StdErr}");
                break;

            default:
                throw new MutationException($"Cannot undo change kind {entry.Kind}.");
        }

        _log.MarkUndone(entry.Id);
    }
}
