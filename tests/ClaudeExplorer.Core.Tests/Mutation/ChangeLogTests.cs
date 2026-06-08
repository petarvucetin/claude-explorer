using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class ChangeLogTests
{
    private static ChangeLogEntry Entry(ScopeKind scope, string desc) => new(
        Id: "",
        Timestamp: "2026-06-07T10:00:00Z",
        Kind: ChangeKind.Edit,
        Scope: scope,
        FilePath: $"/{scope}/settings.json",
        Description: desc,
        Backup: null,
        UndoCommand: null,
        IsUndone: false);

    [Fact]
    public void Record_assigns_a_sequential_id_when_none_is_given()
    {
        var log = new ChangeLog();

        var first = log.Record(Entry(ScopeKind.Project, "a"));
        var second = log.Record(Entry(ScopeKind.Project, "b"));

        Assert.Equal("chg-1", first.Id);
        Assert.Equal("chg-2", second.Id);
        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public void Record_keeps_a_provided_id()
    {
        var log = new ChangeLog();

        var entry = log.Record(Entry(ScopeKind.Project, "a") with { Id = "custom" });

        Assert.Equal("custom", entry.Id);
    }

    [Fact]
    public void MarkUndone_flips_the_flag_on_the_matching_entry()
    {
        var log = new ChangeLog();
        var entry = log.Record(Entry(ScopeKind.Local, "a"));

        log.MarkUndone(entry.Id);

        Assert.True(log.Entries.Single(e => e.Id == entry.Id).IsUndone);
    }

    [Fact]
    public void ByScope_groups_entries_in_precedence_order()
    {
        var log = new ChangeLog();
        log.Record(Entry(ScopeKind.Local, "l"));
        log.Record(Entry(ScopeKind.User, "u"));
        log.Record(Entry(ScopeKind.Project, "p"));

        var groups = log.ByScope();

        Assert.Equal(new[] { ScopeKind.User, ScopeKind.Project, ScopeKind.Local },
            groups.Select(g => g.Key).ToArray());
    }

    [Fact]
    public void Entries_preserves_insertion_order()
    {
        var log = new ChangeLog();
        log.Record(Entry(ScopeKind.Project, "first"));
        log.Record(Entry(ScopeKind.Project, "second"));

        Assert.Equal(new[] { "first", "second" }, log.Entries.Select(e => e.Description).ToArray());
    }
}
