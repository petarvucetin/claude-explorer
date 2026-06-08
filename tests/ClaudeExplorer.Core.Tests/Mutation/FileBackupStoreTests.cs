using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class FileBackupStoreTests
{
    private static FileBackupStore NewStore(InMemoryFileSystem fs)
        => new(fs, fs, "/backups");

    [Fact]
    public void Backup_of_existing_file_reads_content_back()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/.claude/settings.json", "{\"model\":\"x\"}");
        var store = NewStore(fs);

        var entry = store.Backup("/p/.claude/settings.json", originalContent: null, originalExisted: true, "2026-06-07T10:00:00Z");

        Assert.True(entry.OriginalExisted);
        Assert.Equal("/p/.claude/settings.json", entry.OriginalPath);
        Assert.StartsWith("/backups/", entry.BackupPath);
        Assert.Equal("{\"model\":\"x\"}", store.Read(entry));
    }

    [Fact]
    public void Backup_uses_provided_content_when_given()
    {
        var fs = new InMemoryFileSystem();
        var store = NewStore(fs);

        var entry = store.Backup("/p/a.json", originalContent: "provided", originalExisted: true, "2026-06-07T10:00:00Z");

        Assert.Equal("provided", store.Read(entry));
    }

    [Fact]
    public void Backup_of_absent_file_stores_no_content_and_Read_throws()
    {
        var fs = new InMemoryFileSystem();
        var store = NewStore(fs);

        var entry = store.Backup("/p/new.json", originalContent: null, originalExisted: false, "2026-06-07T10:00:00Z");

        Assert.False(entry.OriginalExisted);
        Assert.Throws<InvalidOperationException>(() => store.Read(entry));
    }

    [Fact]
    public void Repeated_backups_with_same_timestamp_do_not_collide()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/a.json", "one");
        var store = NewStore(fs);

        var first = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");
        fs.WriteAllText("/p/a.json", "two");
        var second = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");

        Assert.NotEqual(first.BackupPath, second.BackupPath);
        Assert.Equal("one", store.Read(first));
        Assert.Equal("two", store.Read(second));
    }

    [Fact]
    public void Backup_path_sanitizes_timestamp_punctuation()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/a.json", "x");
        var store = NewStore(fs);

        var entry = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");

        Assert.DoesNotContain(":", entry.BackupPath);
    }
}
