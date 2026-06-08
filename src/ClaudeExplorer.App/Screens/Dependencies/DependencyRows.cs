using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Screens.Dependencies;

public enum DepTone { Ok, Warn, Bad }

public sealed record DependencyRow(
    string Name,
    DepTone Tone,
    string? Version,
    string? Path,
    string ReferencedBy);

public sealed record DependencyView(
    IReadOnlyList<DependencyRow> Rows,
    int Found,
    int Missing,
    int Unverifiable);

public static class DependencyRowsMapper
{
    public static DependencyView Map(DependencyReport report)
    {
        var rows = report.Results.Select(r => new DependencyRow(
            r.Ref.Name,
            Tone(r.Status.Kind),
            r.Status.Version,
            r.Status.Path,
            r.Ref.ReferencedBy.Count > 0
                ? string.Join(", ", r.Ref.ReferencedBy)
                : "")).ToList();

        return new DependencyView(
            rows,
            report.Count(DependencyStatusKind.Found),
            report.Count(DependencyStatusKind.Missing),
            report.Count(DependencyStatusKind.Unverifiable));
    }

    public static DepTone Tone(DependencyStatusKind kind) => kind switch
    {
        DependencyStatusKind.Found => DepTone.Ok,
        DependencyStatusKind.Missing => DepTone.Bad,
        _ => DepTone.Warn,
    };
}
