using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class SettingSpecTests
{
    [Fact]
    public void Registry_defines_scalar_and_list_specs()
    {
        Assert.Contains(SettingSpecs.Scalars, s => s.Key == "model" && s.Strategy == MergeStrategy.ScalarLastWins);
        Assert.Contains(SettingSpecs.Scalars, s => s.Key == "permissions.defaultMode");
        Assert.Contains(SettingSpecs.Lists, s => s.Key == "permissions.allow" && s.Strategy == MergeStrategy.ListUnion);
        Assert.Equal(new[] { "permissions", "allow" }, SettingSpecs.Lists.Single(s => s.Key == "permissions.allow").Path);
    }
}
