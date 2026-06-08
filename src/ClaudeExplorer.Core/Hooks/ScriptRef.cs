namespace ClaudeExplorer.Core.Hooks;

/// <summary>A script file a hook command runs: its resolved absolute path, the highlight.js language
/// id, and whether it currently exists on disk.</summary>
public sealed record ScriptRef(string Path, string Language, bool Exists);
