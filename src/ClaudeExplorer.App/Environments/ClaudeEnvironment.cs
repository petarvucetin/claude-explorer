namespace ClaudeExplorer.App.Environments;

/// <summary>How a Claude environment's config folder is reached.</summary>
public enum EnvironmentKind { Windows, Wsl, Custom }

/// <summary>A discoverable Claude config environment: a user-global <c>.claude</c> root, optionally
/// with its own active project. WSL roots use a <c>\\wsl.localhost\&lt;distro&gt;\…</c> UNC UserDir.</summary>
public sealed record ClaudeEnvironment(
    string Id,
    string Name,
    EnvironmentKind Kind,
    string UserDir,
    string? ProjectDir);
