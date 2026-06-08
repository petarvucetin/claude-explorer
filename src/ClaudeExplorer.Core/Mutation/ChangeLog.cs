using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

public enum ChangeKind { Edit, Install, Uninstall }

/// <summary>
/// One recorded mutation. <see cref="Backup"/> is present for reversible config edits; installs
/// carry an <see cref="UndoCommand"/> (the <c>claude</c> CLI args that reverse them) instead.
/// </summary>
public sealed record ChangeLogEntry(
    string Id,
    string Timestamp,
    ChangeKind Kind,
    ScopeKind Scope,
    string FilePath,
    string Description,
    BackupEntry? Backup,
    IReadOnlyList<string>? UndoCommand,
    bool IsUndone);

/// <summary>
/// In-memory, scope-aware record of every mutation the <see cref="Mutator"/> performs. The UI
/// persists this later; Core only needs it queryable and groupable by scope for review. Insertion
/// order is preserved; <see cref="ByScope"/> groups in precedence order for the change-log screen.
/// </summary>
public sealed class ChangeLog
{
    private readonly List<ChangeLogEntry> _entries = new();
    private int _seq;

    public IReadOnlyList<ChangeLogEntry> Entries => _entries;

    /// <summary>Append an entry. If its <see cref="ChangeLogEntry.Id"/> is empty, a sequential
    /// id (<c>chg-N</c>) is assigned. Returns the stored entry (with its final id).</summary>
    public ChangeLogEntry Record(ChangeLogEntry entry)
    {
        var stored = string.IsNullOrEmpty(entry.Id) ? entry with { Id = $"chg-{++_seq}" } : entry;
        _entries.Add(stored);
        return stored;
    }

    /// <summary>Mark the entry with <paramref name="id"/> as undone (no-op if not found).</summary>
    public void MarkUndone(string id)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Id == id)
            {
                _entries[i] = _entries[i] with { IsUndone = true };
                return;
            }
    }

    /// <summary>Entries grouped by the scope they touched, in precedence order (User→Enterprise).</summary>
    public IReadOnlyList<IGrouping<ScopeKind, ChangeLogEntry>> ByScope()
        => _entries.GroupBy(e => e.Scope).OrderBy(g => (int)g.Key).ToList();
}
