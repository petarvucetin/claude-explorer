namespace ClaudeExplorer.Core.Artifacts;

public static class ArtifactSummary
{
    /// <summary>Frontmatter `description`, else the first non-empty non-heading body line, else null.</summary>
    public static string? Extract(FrontmatterResult frontmatter)
    {
        if (frontmatter.Fields.TryGetValue("description", out var description)
            && !string.IsNullOrWhiteSpace(description))
            return description.Trim();

        string? firstHeading = null;
        foreach (var rawLine in frontmatter.Body.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                var headingText = line.TrimStart('#').Trim();
                if (headingText.Length > 0 && firstHeading is null)
                    firstHeading = headingText;
                continue;
            }
            return line;
        }

        return firstHeading;
    }
}
