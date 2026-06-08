using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SafeMutationServiceTests
{
    private const string Ts = "2026-06-07T10:00:00Z";

    private static SafeMutationService Build(InMemoryFileSystem fs)
    {
        var backups = new FileBackupStore(fs, fs, "/backups");
        return new SafeMutationService(fs, fs, backups, new FakeProcessRunner());
    }

    [Fact]
    public void End_to_end_override_at_project_resolves_previews_applies_and_undoes()
    {
        var fs = new InMemoryFileSystem();
        var service = Build(fs);

        var preview = service.PreviewSettingsEdit(EditMode.OverrideAtProject, "/proj", winner: null, "{ \"model\": \"x\" }");
        Assert.Equal(ScopeKind.Project, preview.Target.Scope);
        Assert.Equal("/proj/.claude/settings.json", preview.Target.FilePath);
        Assert.True(preview.Validation.IsValid);

        var entry = service.ApplyEdit(preview, Ts);
        Assert.Equal("{ \"model\": \"x\" }", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Single(service.ChangeLog.Entries);

        service.Undo(entry);
        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
        Assert.True(service.ChangeLog.Entries.Single().IsUndone);
    }

    [Fact]
    public void ResolveTarget_edit_winner_follows_provenance()
    {
        var service = Build(new InMemoryFileSystem());
        var winner = new SettingOrigin(ScopeKind.User, "/home/.claude/settings.json", "model");

        var target = service.ResolveTarget(EditMode.EditWinner, "/proj", winner);

        Assert.Equal(ScopeKind.User, target.Scope);
        Assert.Equal("/home/.claude/settings.json", target.FilePath);
    }
}
