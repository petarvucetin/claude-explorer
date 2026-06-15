using ClaudeExplorer.Core.Artifacts;
using Markdig;

namespace ClaudeExplorer.Core.Rendering;

/// <summary>
/// Markdown rendered to HTML, split from any leading YAML-style frontmatter block.
/// <paramref name="Fields"/> carries the parsed frontmatter (empty when none); <paramref name="Html"/>
/// is the HTML for the body only.
/// </summary>
public sealed record RenderedMarkdown(IReadOnlyDictionary<string, string> Fields, string Html);

/// <summary>
/// Renders markdown (CLAUDE.md, skill/command/subagent files) to HTML for the formatted viewer.
/// GitHub-flavored extras (pipe tables, autolinks, task lists) are enabled; <b>raw inline HTML is
/// disabled</b> so file content cannot inject <c>&lt;script&gt;</c> or other live markup — such tags
/// are escaped instead.
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    /// <summary>Render a markdown string to HTML. Returns "" for null/empty input.</summary>
    public string ToHtml(string? markdown)
        => string.IsNullOrEmpty(markdown) ? "" : Markdown.ToHtml(markdown, _pipeline);

    /// <summary>
    /// Split frontmatter off <paramref name="content"/> and render the remaining body to HTML.
    /// </summary>
    public RenderedMarkdown Render(string? content)
    {
        var fm = Frontmatter.Parse(content);
        return new RenderedMarkdown(fm.Fields, ToHtml(fm.Body));
    }
}
