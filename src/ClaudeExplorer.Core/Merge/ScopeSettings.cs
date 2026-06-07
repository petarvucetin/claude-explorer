using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

/// <summary>A parsed settings object tagged with its scope and source path.</summary>
public sealed record ScopeSettings(ScopeKind Scope, string FilePath, JsonObject Root);
