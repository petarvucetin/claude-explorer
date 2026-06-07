namespace ClaudeExplorer.Core.Model;

/// <summary>A settings file located on disk for a given scope.</summary>
public sealed record ConfigFile(ScopeKind Scope, string Path);
