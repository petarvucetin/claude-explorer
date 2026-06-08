using System.Text.Json.Nodes;
using ClaudeExplorer.App.Screens.EffectiveConfig;
using ClaudeExplorer.Core.Model;
using CoreEffectiveConfig = ClaudeExplorer.Core.Model.EffectiveConfig;

namespace ClaudeExplorer.App.Tests.Screens;

public class EffectiveConfigRowsTests
{
    // Helper: build a contribution
    private static SettingContribution Contrib(ScopeKind scope, string filePath, JsonNode? value)
        => new(new SettingOrigin(scope, filePath, "model"), value);

    [Fact]
    public void Scalar_conflict_user_wins_over_project()
    {
        // User sets model=opus (wins), project sets model=sonnet (overridden → conflict)
        var userContrib  = Contrib(ScopeKind.User,    "/user/settings.json",    JsonValue.Create("opus"));
        var projContrib  = Contrib(ScopeKind.Project, "/project/settings.json", JsonValue.Create("sonnet"));

        var setting = new EffectiveSetting(
            "model",
            MergeStrategy.ScalarLastWins,
            JsonValue.Create("opus"),
            new SettingOrigin(ScopeKind.User, "/user/settings.json", "model"),
            new[] { projContrib, userContrib },
            HasConflict: true);

        var config = new CoreEffectiveConfig(new[] { setting });
        var view = EffectiveConfigMapper.Map(config);

        Assert.Equal(1, view.ConflictCount);
        var row = Assert.Single(view.Rows);

        Assert.True(row.HasConflict);
        Assert.Equal("scalar · last-wins", row.MergeLabel);

        // User cell is the winner
        var userCell = row.Cells[ScopeKind.User];
        Assert.True(userCell.Present);
        Assert.True(userCell.IsWinner);
        Assert.False(userCell.IsOverridden);
        Assert.Equal("\"opus\"", userCell.Display);

        // Project cell is present but overridden
        var projCell = row.Cells[ScopeKind.Project];
        Assert.True(projCell.Present);
        Assert.False(projCell.IsWinner);
        Assert.True(projCell.IsOverridden);
        Assert.Equal("\"sonnet\"", projCell.Display);

        // Enterprise and Local are absent
        Assert.False(row.Cells[ScopeKind.Enterprise].Present);
        Assert.False(row.Cells[ScopeKind.Local].Present);

        // Effective display is the winning value
        Assert.Equal("\"opus\"", row.EffectiveDisplay);

        // Winner origin
        Assert.NotNull(row.Winner);
        Assert.Equal(ScopeKind.User, row.Winner!.Scope);
    }

    [Fact]
    public void List_setting_merge_label_is_merged()
    {
        var setting = new EffectiveSetting(
            "permissions.allow",
            MergeStrategy.ListUnion,
            JsonValue.Create("[]"),
            null,
            Array.Empty<SettingContribution>(),
            HasConflict: false);

        var view = EffectiveConfigMapper.Map(new CoreEffectiveConfig(new[] { setting }));
        var row = Assert.Single(view.Rows);

        Assert.Equal("merged · union", row.MergeLabel);
        Assert.False(row.HasConflict);
        Assert.Equal(0, view.ConflictCount);
    }

    [Fact]
    public void Missing_scopes_are_not_present()
    {
        var setting = new EffectiveSetting(
            "outputStyle",
            MergeStrategy.ScalarLastWins,
            JsonValue.Create("json"),
            new SettingOrigin(ScopeKind.Project, "/p/settings.json", "outputStyle"),
            new[] { Contrib(ScopeKind.Project, "/p/settings.json", JsonValue.Create("json")) },
            HasConflict: false);

        var view = EffectiveConfigMapper.Map(new CoreEffectiveConfig(new[] { setting }));
        var row = Assert.Single(view.Rows);

        Assert.False(row.Cells[ScopeKind.User].Present);
        Assert.False(row.Cells[ScopeKind.Enterprise].Present);
        Assert.False(row.Cells[ScopeKind.Local].Present);
        Assert.True(row.Cells[ScopeKind.Project].Present);
        Assert.True(row.Cells[ScopeKind.Project].IsWinner);
    }

    [Fact]
    public void Merge_label_array_concat()
    {
        var s = EffectiveConfigMapper.MergeLabel(MergeStrategy.ArrayConcat);
        Assert.Equal("merged · concat", s);
    }

    [Fact]
    public void Empty_config_returns_empty_view()
    {
        var view = EffectiveConfigMapper.Map(new CoreEffectiveConfig(Array.Empty<EffectiveSetting>()));
        Assert.Empty(view.Rows);
        Assert.Equal(0, view.ConflictCount);
    }
}
