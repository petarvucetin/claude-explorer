namespace ClaudeExplorer.Core.Mutation;

public enum DiffKind { Context, Added, Removed }

/// <summary>
/// One line of a diff. <see cref="OldLine"/> / <see cref="NewLine"/> are 1-based line numbers in
/// the before / after text, or <c>null</c> when the line does not exist on that side.
/// </summary>
public sealed record DiffLine(DiffKind Kind, string Text, int? OldLine, int? NewLine);

public sealed record Diff(IReadOnlyList<DiffLine> Lines)
{
    public bool HasChanges => Lines.Any(l => l.Kind != DiffKind.Context);
    public int Added => Lines.Count(l => l.Kind == DiffKind.Added);
    public int Removed => Lines.Count(l => l.Kind == DiffKind.Removed);
}
