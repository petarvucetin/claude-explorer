namespace ClaudeExplorer.Core.Mutation;

/// <summary>Outcome of validating proposed file content before it is written.</summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Fail(params string[] errors) => new(false, errors);
}
