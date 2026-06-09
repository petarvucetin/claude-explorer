using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SafeMutationServiceDeleteTests
{
    [Fact]
    public void ApplyDelete_removes_file_records_entry_and_undo_restores()
    {
        var fs = new InMemoryFileSystem();
        var backups = new InMemoryFileSystem();
        fs.AddFile("/proj/CLAUDE.md", "# rules");
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(backups, backups, "/bk"), new FakeProcessRunner());

        var entry = svc.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/CLAUDE.md"), "2026-06-08T00:00:00Z", "Delete CLAUDE.md");
        Assert.False(fs.FileExists("/proj/CLAUDE.md"));
        Assert.Single(svc.ChangeLog.Entries);

        svc.Undo(entry);
        Assert.True(fs.FileExists("/proj/CLAUDE.md"));
        Assert.Equal("# rules", fs.ReadAllText("/proj/CLAUDE.md"));
    }
}
