using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class MutatorDeleteTests
{
    private const string Ts = "2026-06-08T00:00:00Z";

    private static (Mutator m, ChangeLog log, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        var backups = new InMemoryFileSystem();
        var log = new ChangeLog();
        var m = new Mutator(fs, fs, new FileBackupStore(backups, backups, "/bk"), log, new FakeProcessRunner());
        return (m, log, fs);
    }

    [Fact]
    public void ApplyDelete_backs_up_then_removes_the_file()
    {
        var (m, log, fs) = Build();
        fs.AddFile("/proj/.claude/commands/deploy.md", "# deploy");

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/deploy.md"), Ts, "Delete deploy");

        Assert.False(fs.FileExists("/proj/.claude/commands/deploy.md"));
        Assert.Equal(ChangeKind.Delete, entry.Kind);
        Assert.NotNull(entry.Backup);
        Assert.True(entry.Backup!.OriginalExisted);
        Assert.Single(log.Entries);
    }

    [Fact]
    public void Undo_of_a_delete_recreates_the_file_with_original_content()
    {
        var (m, log, fs) = Build();
        fs.AddFile("/proj/.claude/commands/deploy.md", "# deploy v1");

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/deploy.md"), Ts, "Delete deploy");
        m.Undo(entry);

        Assert.True(fs.FileExists("/proj/.claude/commands/deploy.md"));
        Assert.Equal("# deploy v1", fs.ReadAllText("/proj/.claude/commands/deploy.md"));
        Assert.True(log.Entries.Single().IsUndone);
    }

    [Fact]
    public void ApplyDelete_of_a_missing_file_is_a_noop_delete_that_records_an_entry()
    {
        var (m, log, fs) = Build();

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/ghost.md"), Ts, "Delete ghost");

        Assert.False(fs.FileExists("/proj/.claude/commands/ghost.md"));
        Assert.NotNull(entry.Backup);
        Assert.False(entry.Backup!.OriginalExisted);
        // Undo of a delete whose original never existed must not recreate anything.
        m.Undo(entry);
        Assert.False(fs.FileExists("/proj/.claude/commands/ghost.md"));
    }
}
