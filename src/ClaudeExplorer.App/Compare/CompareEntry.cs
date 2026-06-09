namespace ClaudeExplorer.App.Compare;

/// <summary>A comparable map value: the canonical <see cref="Display"/> string (used for diff
/// classification AND shown in the table) plus the resolved <see cref="Path"/> on disk and the
/// file <see cref="Content"/> when applicable. Path/content are what a copy/move needs to build a
/// <c>CopyRequest</c>; they are empty for value-only categories (Settings/MCP/Plugins/Deps).</summary>
public sealed record CompareEntry(string Display, string Path = "", string Content = "");
