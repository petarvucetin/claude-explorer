using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Model;

public class EffectiveSettingTests
{
    [Fact]
    public void Find_returns_setting_by_key()
    {
        var origin = new SettingOrigin(ScopeKind.User, "/u/settings.json", "model");
        var contrib = new SettingContribution(origin, JsonValue.Create("opus"));
        var setting = new EffectiveSetting(
            Key: "model",
            Strategy: MergeStrategy.ScalarLastWins,
            Value: JsonValue.Create("opus"),
            Winner: origin,
            Contributions: new[] { contrib },
            HasConflict: false);

        var config = new EffectiveConfig(new[] { setting });

        Assert.Same(setting, config.Find("model"));
        Assert.Null(config.Find("nope"));
    }
}
