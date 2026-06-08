namespace ClaudeExplorer.App.Compare;

/// <summary>A is the left environment, B the right.</summary>
public enum DiffStatus { Same, Differs, OnlyA, OnlyB }

public sealed record CompareRow(string Key, DiffStatus Status, string? ValueA, string? ValueB);

public sealed record CompareCategory(string Name, IReadOnlyList<CompareRow> Rows)
{
    public int Same => Rows.Count(r => r.Status == DiffStatus.Same);
    public int Differs => Rows.Count(r => r.Status == DiffStatus.Differs);
    public int OnlyA => Rows.Count(r => r.Status == DiffStatus.OnlyA);
    public int OnlyB => Rows.Count(r => r.Status == DiffStatus.OnlyB);
}

public sealed record EnvironmentComparison(IReadOnlyList<CompareCategory> Categories)
{
    public CompareCategory? Find(string name) => Categories.FirstOrDefault(c => c.Name == name);
}
