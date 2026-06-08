namespace ClaudeExplorer.Core.Mutation;

/// <summary>A snapshot of a file taken before it was mutated, so the change can be reversed.
/// When <see cref="OriginalExisted"/> is false the file was newly created and undo deletes it.</summary>
public sealed record BackupEntry(
    string OriginalPath,
    string BackupPath,
    string Timestamp,
    bool OriginalExisted);

/// <summary>Stores pre-mutation file snapshots and reads them back for undo.</summary>
public interface IBackupStore
{
    /// <summary>
    /// Snapshot <paramref name="originalPath"/>. Pass <paramref name="originalExisted"/> = false
    /// (with <paramref name="originalContent"/> = null) when the file does not yet exist; undo of
    /// such a change deletes the created file. When the file exists, <paramref name="originalContent"/>
    /// may be supplied to avoid a re-read, or left null to read it from the store's file system.
    /// </summary>
    BackupEntry Backup(string originalPath, string? originalContent, bool originalExisted, string timestamp);

    /// <summary>Read the snapshotted content of a backup. Throws if the original did not exist.</summary>
    string Read(BackupEntry entry);
}
