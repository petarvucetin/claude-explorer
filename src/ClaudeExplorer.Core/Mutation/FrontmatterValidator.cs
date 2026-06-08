using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Validates the YAML-style frontmatter of a markdown artifact (skill / command / subagent)
/// before it is written: the document must open with a <c>---</c> frontmatter block and contain
/// every required field with a non-empty value. Reuses the discovery <see cref="Frontmatter"/>
/// parser so validation and discovery agree on what a well-formed block is. Defaults to requiring
/// <c>name</c> and <c>description</c>.
/// </summary>
public sealed class FrontmatterValidator
{
    private readonly IReadOnlyList<string> _requiredFields;

    public FrontmatterValidator(params string[] requiredFields)
        => _requiredFields = requiredFields.Length > 0 ? requiredFields : new[] { "name", "description" };

    public ValidationResult Validate(string content)
    {
        var text = (content ?? "").TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return ValidationResult.Fail("Document must begin with a \"---\" frontmatter block.");

        var parsed = Frontmatter.Parse(content);
        var errors = new List<string>();
        foreach (var field in _requiredFields)
            if (!parsed.Fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add($"Frontmatter is missing required field \"{field}\".");

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }
}
