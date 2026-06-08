namespace ClaudeExplorer.App.Compare;

public enum EndpointKind { Base, Project }

/// <summary>A comparison endpoint: a base (an environment's <c>~/.claude</c> root) or a project
/// folder. <see cref="ReadUserDir"/>/<see cref="ReadProjectDir"/> are the (userDir, projectDir) the
/// Core readers take to read this endpoint's OWNED config — a base reads as user-only, a project reads
/// as project-only (no base overlay), so copy acts on the files that actually live there.</summary>
public sealed record CompareEndpoint(string Id, EndpointKind Kind, string Label, string UserDir, string? ProjectDir)
{
    public string ReadUserDir => Kind == EndpointKind.Base ? UserDir : "";
    public string ReadProjectDir => Kind == EndpointKind.Base ? "" : (ProjectDir ?? "");

    public static CompareEndpoint Base(string id, string label, string userDir)
        => new($"base:{id}", EndpointKind.Base, label, userDir, null);

    public static CompareEndpoint Project(string id, string label, string projectDir)
        => new($"proj:{id}", EndpointKind.Project, label, "", projectDir);
}
