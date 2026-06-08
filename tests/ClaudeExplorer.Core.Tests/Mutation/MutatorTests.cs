using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class MutatorTests
{
    private const string Ts = "2026-06-07T10:00:00Z";

    private static (Mutator mutator, InMemoryFileSystem fs, ChangeLog log, FakeProcessRunner runner) Build(InMemoryFileSystem? seed = null)
    {
        var fs = seed ?? new InMemoryFileSystem();
        var backups = new FileBackupStore(fs, fs, "/backups");
        var log = new ChangeLog();
        var runner = new FakeProcessRunner();
        var mutator = new Mutator(fs, fs, backups, log, runner);
        return (mutator, fs, log, runner);
    }

    private static ResolvedTarget ProjectTarget(string projectDir = "/proj")
        => new(ScopeKind.Project, $"{projectDir}/.claude/settings.json");

    [Fact]
    public void PreviewEdit_of_new_file_reports_no_old_content_and_all_additions()
    {
        var (mutator, _, _, _) = Build();

        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{\n  \"model\": \"x\"\n}");

        Assert.False(preview.TargetExisted);
        Assert.Equal("", preview.OldContent);
        Assert.True(preview.Diff.HasChanges);
        Assert.True(preview.Validation.IsValid);
    }

    [Fact]
    public void ApplyEdit_writes_content_and_records_a_change_entry()
    {
        var (mutator, fs, log, _) = Build();
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"x\" }");

        var entry = mutator.ApplyEdit(preview, Ts);

        Assert.Equal("{ \"model\": \"x\" }", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Equal(ChangeKind.Edit, entry.Kind);
        Assert.Equal(ScopeKind.Project, entry.Scope);
        Assert.Same(entry, Assert.Single(log.Entries));
    }

    [Fact]
    public void ApplyEdit_refuses_invalid_content_and_writes_nothing()
    {
        var (mutator, fs, log, _) = Build();
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": 123 }");

        Assert.Throws<MutationException>(() => mutator.ApplyEdit(preview, Ts));
        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Undo_of_edit_on_preexisting_file_restores_original_content()
    {
        var seed = new InMemoryFileSystem().AddFile("/proj/.claude/settings.json", "{ \"model\": \"old\" }");
        var (mutator, fs, _, _) = Build(seed);
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"new\" }");
        var entry = mutator.ApplyEdit(preview, Ts);
        Assert.Equal("{ \"model\": \"new\" }", fs.ReadAllText("/proj/.claude/settings.json"));

        mutator.Undo(entry);

        Assert.Equal("{ \"model\": \"old\" }", fs.ReadAllText("/proj/.claude/settings.json"));
    }

    [Fact]
    public void Undo_of_edit_that_created_the_file_deletes_it()
    {
        var (mutator, fs, _, _) = Build();
        var entry = mutator.ApplyEdit(mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"x\" }"), Ts);
        Assert.True(fs.FileExists("/proj/.claude/settings.json"));

        mutator.Undo(entry);

        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
    }

    [Fact]
    public void Undo_marks_the_entry_undone_and_a_second_undo_throws()
    {
        var (mutator, _, log, _) = Build();
        var entry = mutator.ApplyEdit(mutator.PreviewSettingsEdit(ProjectTarget(), "{}"), Ts);

        mutator.Undo(entry);

        Assert.True(log.Entries.Single().IsUndone);
        Assert.Throws<MutationException>(() => mutator.Undo(entry));
    }

    [Fact]
    public void Install_runs_the_claude_cli_and_records_an_install_entry()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddVersion("claude", "ok"); // exit 0
        var request = new InstallRequest(
            "acme-skill", ScopeKind.User,
            InstallArgs: new[] { "plugin", "install", "acme-skill" },
            UninstallArgs: new[] { "plugin", "uninstall", "acme-skill" });

        var entry = mutator.Install(request, Ts);

        Assert.Equal(ChangeKind.Install, entry.Kind);
        Assert.Equal("acme-skill", entry.FilePath);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("claude", call.Executable);
        Assert.Equal(new[] { "plugin", "install", "acme-skill" }, call.Arguments);
        Assert.Same(entry, Assert.Single(log.Entries));
    }

    [Fact]
    public void Install_throws_when_the_cli_exits_nonzero()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddResult("claude", new ClaudeExplorer.Core.Dependencies.ProcessResult(1, "", "boom"));
        var request = new InstallRequest("bad", ScopeKind.User, new[] { "plugin", "install", "bad" }, new[] { "plugin", "uninstall", "bad" });

        var ex = Assert.Throws<MutationException>(() => mutator.Install(request, Ts));

        Assert.Contains("boom", ex.Message);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Undo_of_install_runs_the_uninstall_command()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddVersion("claude", "ok");
        var request = new InstallRequest("acme", ScopeKind.User, new[] { "plugin", "install", "acme" }, new[] { "plugin", "uninstall", "acme" });
        var entry = mutator.Install(request, Ts);

        mutator.Undo(entry);

        Assert.Equal(2, runner.Invocations.Count);
        Assert.Equal(new[] { "plugin", "uninstall", "acme" }, runner.Invocations[1].Arguments);
        Assert.True(log.Entries.Single().IsUndone);
    }
}
