using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// File-backed <see cref="IBackupStore"/>. Each snapshot is written under
/// <c>{backupRoot}/{sanitizedTimestamp}-{n}-{fileName}.bak</c>, where <c>n</c> is a monotonic
/// counter so repeated backups (even within one timestamp) never collide. Uses the same
/// <see cref="IFileSystem"/> / <see cref="IFileWriter"/> seams as the rest of Core, so it is fully
/// testable against the in-memory fake and never touches the real machine in tests.
/// </summary>
public sealed class FileBackupStore : IBackupStore
{
    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly string _backupRoot;
    private int _counter;

    public FileBackupStore(IFileSystem fs, IFileWriter writer, string backupRoot)
    {
        _fs = fs;
        _writer = writer;
        _backupRoot = backupRoot.Replace('\\', '/').TrimEnd('/');
    }

    public BackupEntry Backup(string originalPath, string? originalContent, bool originalExisted, string timestamp)
    {
        var normalized = originalPath.Replace('\\', '/');

        if (originalExisted)
        {
            var content = originalContent ?? _fs.ReadAllText(normalized);
            var name = normalized.Substring(normalized.LastIndexOf('/') + 1);
            var backupPath = $"{_backupRoot}/{Sanitize(timestamp)}-{++_counter}-{name}.bak";
            _writer.WriteAllText(backupPath, content);
            return new BackupEntry(normalized, backupPath, timestamp, true);
        }

        // Nothing to snapshot; record the absence so undo can delete the created file.
        return new BackupEntry(normalized, "", timestamp, false);
    }

    public string Read(BackupEntry entry)
    {
        if (!entry.OriginalExisted)
            throw new InvalidOperationException(
                $"Backup for {entry.OriginalPath} has no content: the file did not exist when snapshotted.");
        return _fs.ReadAllText(entry.BackupPath);
    }

    private static string Sanitize(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        return new string(chars);
    }
}
