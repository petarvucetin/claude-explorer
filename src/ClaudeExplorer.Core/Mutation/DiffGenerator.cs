namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Produces a line-oriented diff between two text blobs using a longest-common-subsequence
/// backtrace. Deterministic; renders the before / after preview for the safe-edit flow. Line
/// endings are normalized to <c>\n</c> before comparison.
/// </summary>
public sealed class DiffGenerator
{
    public Diff Generate(string before, string after)
    {
        var a = SplitLines(before);
        var b = SplitLines(after);

        // lcs[i, j] = length of the longest common subsequence of a[i..] and b[j..].
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (int i = a.Length - 1; i >= 0; i--)
            for (int j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var lines = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                lines.Add(new DiffLine(DiffKind.Context, a[x], x + 1, y + 1));
                x++; y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                lines.Add(new DiffLine(DiffKind.Removed, a[x], x + 1, null));
                x++;
            }
            else
            {
                lines.Add(new DiffLine(DiffKind.Added, b[y], null, y + 1));
                y++;
            }
        }
        while (x < a.Length) { lines.Add(new DiffLine(DiffKind.Removed, a[x], x + 1, null)); x++; }
        while (y < b.Length) { lines.Add(new DiffLine(DiffKind.Added, b[y], null, y + 1)); y++; }

        return new Diff(lines);
    }

    private static string[] SplitLines(string text)
        => (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
}
