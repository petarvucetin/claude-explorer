using ClaudeExplorer.App.Screens.Hooks;

namespace ClaudeExplorer.App.Tests.Screens;

public class MatcherChipsTests
{
    [Fact]
    public void Star_matcher_becomes_a_single_any_chip()
    {
        var chips = HookMatcher.Chips("*");
        var chip = Assert.Single(chips);
        Assert.True(chip.IsAny);
        Assert.Equal("∗ any tool", chip.Text);
    }

    [Fact]
    public void Empty_matcher_is_treated_as_any()
    {
        var chip = Assert.Single(HookMatcher.Chips(""));
        Assert.True(chip.IsAny);
    }

    [Fact]
    public void Pipe_list_splits_into_one_chip_per_tool()
    {
        var chips = HookMatcher.Chips("Bash|Read|Write");
        Assert.Equal(new[] { "Bash", "Read", "Write" }, chips.Select(c => c.Text));
        Assert.All(chips, c => Assert.False(c.IsAny));
    }

    [Fact]
    public void Single_tool_is_one_chip()
        => Assert.Equal("Edit", Assert.Single(HookMatcher.Chips("Edit")).Text);

    [Fact]
    public void Regex_token_is_passed_through_verbatim()
        => Assert.Equal("Notebook.*", Assert.Single(HookMatcher.Chips("Notebook.*")).Text);
}
