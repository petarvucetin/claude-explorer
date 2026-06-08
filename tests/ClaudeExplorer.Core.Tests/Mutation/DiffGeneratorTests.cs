using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class DiffGeneratorTests
{
    private static readonly DiffGenerator Gen = new();

    [Fact]
    public void Identical_text_has_only_context_lines_and_no_changes()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nb\nc");

        Assert.False(diff.HasChanges);
        Assert.All(diff.Lines, l => Assert.Equal(DiffKind.Context, l.Kind));
        Assert.Equal(3, diff.Lines.Count);
    }

    [Fact]
    public void A_changed_middle_line_is_a_remove_then_add()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nB\nc");

        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.Added);
        Assert.Equal(1, diff.Removed);
        // Order: context a, remove b, add B, context c
        Assert.Collection(diff.Lines,
            l => Assert.Equal((DiffKind.Context, "a"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Removed, "b"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Added, "B"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Context, "c"), (l.Kind, l.Text)));
    }

    [Fact]
    public void Appended_lines_are_additions_with_null_old_line_numbers()
    {
        var diff = Gen.Generate("a", "a\nb");

        var added = Assert.Single(diff.Lines, l => l.Kind == DiffKind.Added);
        Assert.Equal("b", added.Text);
        Assert.Null(added.OldLine);
        Assert.Equal(2, added.NewLine);
    }

    [Fact]
    public void Removed_lines_are_removals_with_null_new_line_numbers()
    {
        var diff = Gen.Generate("a\nb", "a");

        var removed = Assert.Single(diff.Lines, l => l.Kind == DiffKind.Removed);
        Assert.Equal("b", removed.Text);
        Assert.Equal(2, removed.OldLine);
        Assert.Null(removed.NewLine);
    }

    [Fact]
    public void Context_lines_carry_both_line_numbers()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nB\nc");

        var c = Assert.Single(diff.Lines, l => l.Text == "c" && l.Kind == DiffKind.Context);
        Assert.Equal(3, c.OldLine);
        Assert.Equal(3, c.NewLine);
    }

    [Fact]
    public void Crlf_is_normalized_before_diffing()
    {
        var diff = Gen.Generate("a\r\nb", "a\nb");

        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void Empty_before_yields_only_additions_no_phantom_removed_line()
    {
        // A brand-new file (empty "before") must not produce a spurious removed empty line.
        var diff = Gen.Generate("", "a\nb");

        Assert.DoesNotContain(diff.Lines, l => l.Kind == DiffKind.Removed);
        Assert.Equal(2, diff.Added);
        Assert.Collection(diff.Lines,
            l => Assert.Equal((DiffKind.Added, "a"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Added, "b"), (l.Kind, l.Text)));
    }

    [Fact]
    public void Empty_after_yields_only_removals_no_phantom_added_line()
    {
        var diff = Gen.Generate("a\nb", "");

        Assert.DoesNotContain(diff.Lines, l => l.Kind == DiffKind.Added);
        Assert.Equal(2, diff.Removed);
    }
}
