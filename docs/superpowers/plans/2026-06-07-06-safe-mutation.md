# Phase 6 — Safe-Mutation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Core safe-mutation layer that applies config edits and catalog installs
safely and reversibly — scope-target resolution, validation, diff preview, timestamped
backups, a scope-aware change log, and one-click undo.

**Architecture:** A new `Mutation/` namespace in `ClaudeExplorer.Core`. Config edits are direct
file writes guarded by validate → backup → write → record; installs delegate to the `claude`
CLI through the existing Phase-3 `IProcessRunner`. A new write seam `IFileWriter` keeps the
read-only engines free of mutation capability. Everything is TDD against the in-memory fakes —
tests never touch the real machine. A thin `SafeMutationService` façade wires it together for the
UI (Phases 7–8).

**Tech Stack:** .NET 10, C#, `System.Text.Json.Nodes`, xUnit. No new NuGet dependencies.

**Conventions (carried from Phases 1–5):**
- xUnit only (no FluentAssertions). Test project has global usings for `System`,
  `System.Collections.Generic`, `System.IO`, `System.Linq`, `Xunit` — do NOT re-import those.
- Forward-slash paths in code. Names/keys matched case-sensitively (ordinal) by design.
- Testability seams only: `IFileSystem` (read), `IProcessRunner` (Phase 3), and the new
  `IFileWriter`. `Physical*` impls live in Core and are NOT unit-tested (they touch the machine,
  mirroring `PhysicalFileSystem`/`PhysicalProcessRunner`). Fakes live in
  `tests/.../Fakes/` and are used everywhere else.
- Solution: `ClaudeExplorer.slnx`. Run `dotnet` via **PowerShell** (not the Bash tool).
- Commit per task; messages end with the `Co-Authored-By: Claude …` trailer used in prior phases.

**Hard contract (Phase-6 spec — all required, each mapped to a task):**
scope-target picker (Task 2) · diff preview (Task 5) · schema/frontmatter validation (Tasks 3–4)
· automatic timestamped backups (Task 6) · one-click undo/restore (Task 8) · reviewable
scope-aware change log (Task 7).

---

### Task 1: `IFileWriter` write seam + fake support

**Files:**
- Create: `src/ClaudeExplorer.Core/Io/IFileWriter.cs`
- Modify: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs` (add `IFileWriter`)
- Test: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileWriterTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileWriterTests.cs`:

```csharp
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileWriterTests
{
    [Fact]
    public void WriteAllText_creates_a_readable_file()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.WriteAllText("/p/.claude/settings.json", "{}");

        Assert.True(fs.FileExists("/p/.claude/settings.json"));
        Assert.Equal("{}", fs.ReadAllText("/p/.claude/settings.json"));
    }

    [Fact]
    public void WriteAllText_overwrites_existing_content()
    {
        var fs = new InMemoryFileSystem().AddFile("/a.json", "old");
        IFileWriter writer = fs;

        writer.WriteAllText("/a.json", "new");

        Assert.Equal("new", fs.ReadAllText("/a.json"));
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var fs = new InMemoryFileSystem().AddFile("/a.json", "x");
        IFileWriter writer = fs;

        writer.Delete("/a.json");

        Assert.False(fs.FileExists("/a.json"));
    }

    [Fact]
    public void Delete_is_a_no_op_when_file_is_absent()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.Delete("/missing.json"); // must not throw

        Assert.False(fs.FileExists("/missing.json"));
    }

    [Fact]
    public void Writes_normalize_backslashes_so_reads_via_forward_slashes_match()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.WriteAllText(@"C:\p\.claude\settings.json", "{}");

        Assert.True(fs.FileExists("C:/p/.claude/settings.json"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (PowerShell): `dotnet test --nologo`
Expected: FAIL — `InMemoryFileSystem` does not implement `IFileWriter` (compile error), `IFileWriter` not found.

- [ ] **Step 3: Create `IFileWriter` + `PhysicalFileWriter`**

`src/ClaudeExplorer.Core/Io/IFileWriter.cs`:

```csharp
namespace ClaudeExplorer.Core.Io;

/// <summary>
/// Write-side companion to <see cref="IFileSystem"/>. Kept separate so the read-only engines
/// (discovery, merge, catalog, recommendations) never take a dependency on mutation capability —
/// only the Phase-6 safe-mutation layer accepts an <see cref="IFileWriter"/>.
/// </summary>
public interface IFileWriter
{
    /// <summary>Write <paramref name="content"/> to <paramref name="path"/>, creating parent
    /// directories and overwriting any existing file.</summary>
    void WriteAllText(string path, string content);

    /// <summary>Delete <paramref name="path"/> if it exists; a no-op when it is absent.</summary>
    void Delete(string path);
}

public sealed class PhysicalFileWriter : IFileWriter
{
    public void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
```

- [ ] **Step 4: Extend the in-memory fake to implement `IFileWriter`**

In `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs`, change the class declaration and
add two members. The class currently reads:

```csharp
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
```

Change the declaration to also implement `IFileWriter`:

```csharp
public sealed class InMemoryFileSystem : IFileSystem, IFileWriter
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
```

Then add these two methods inside the class (e.g. just after `AddFile`):

```csharp
    public void WriteAllText(string path, string content) => _files[Normalize(path)] = content;

    public void Delete(string path) => _files.Remove(Normalize(path));
```

(`Normalize` already exists in the class and converts `\` → `/`.)

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS — all 149 prior tests still pass plus the 5 new ones (154 total).

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeExplorer.Core/Io/IFileWriter.cs tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileWriterTests.cs
git commit -m "feat(core): IFileWriter write seam + in-memory fake support"
```

---

### Task 2: `ScopeTarget` resolution (edit winner vs override at Project/Local)

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/ScopeTarget.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/ScopeTargetResolverTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/ScopeTargetResolverTests.cs`:

```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class ScopeTargetResolverTests
{
    private static readonly ScopeTargetResolver Resolver = new();

    [Fact]
    public void EditWinner_follows_the_current_winning_origin()
    {
        var winner = new SettingOrigin(ScopeKind.User, "/home/u/.claude/settings.json", "model");

        var target = Resolver.Resolve(EditMode.EditWinner, "/work/proj", winner);

        Assert.Equal(ScopeKind.User, target.Scope);
        Assert.Equal("/home/u/.claude/settings.json", target.FilePath);
    }

    [Fact]
    public void EditWinner_throws_when_setting_is_not_defined_anywhere()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Resolver.Resolve(EditMode.EditWinner, "/work/proj", winner: null));

        Assert.Contains("override", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverrideAtProject_targets_project_settings_regardless_of_winner()
    {
        var winner = new SettingOrigin(ScopeKind.User, "/home/u/.claude/settings.json", "model");

        var target = Resolver.Resolve(EditMode.OverrideAtProject, "/work/proj", winner);

        Assert.Equal(ScopeKind.Project, target.Scope);
        Assert.Equal("/work/proj/.claude/settings.json", target.FilePath);
    }

    [Fact]
    public void OverrideAtLocal_targets_local_settings_regardless_of_winner()
    {
        var target = Resolver.Resolve(EditMode.OverrideAtLocal, "/work/proj", winner: null);

        Assert.Equal(ScopeKind.Local, target.Scope);
        Assert.Equal("/work/proj/.claude/settings.local.json", target.FilePath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `EditMode`, `ScopeTargetResolver`, `ResolvedTarget` not found.

- [ ] **Step 3: Implement `ScopeTarget.cs`**

`src/ClaudeExplorer.Core/Mutation/ScopeTarget.cs`:

```csharp
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>How the user wants an edit to land relative to the current winning value.</summary>
public enum EditMode
{
    /// <summary>Write into the file/scope that currently provides the winning value.</summary>
    EditWinner,

    /// <summary>Create or update an override in the project <c>settings.json</c>.</summary>
    OverrideAtProject,

    /// <summary>Create or update an override in the project <c>settings.local.json</c>.</summary>
    OverrideAtLocal,
}

/// <summary>The concrete destination an edit resolves to.</summary>
public sealed record ResolvedTarget(ScopeKind Scope, string FilePath);

/// <summary>
/// Resolves an <see cref="EditMode"/> plus workspace context to the concrete settings file an
/// edit will be written to. "Edit winner" follows the current provenance; the override modes
/// always target the project / local settings files regardless of where the winner lives. Paths
/// use forward slashes and mirror <c>SettingsLocator</c>'s layout.
/// </summary>
public sealed class ScopeTargetResolver
{
    public ResolvedTarget Resolve(EditMode mode, string projectDir, SettingOrigin? winner)
    {
        var proj = projectDir.Replace('\\', '/').TrimEnd('/');
        return mode switch
        {
            EditMode.EditWinner => winner is not null
                ? new ResolvedTarget(winner.Scope, winner.FilePath)
                : throw new InvalidOperationException(
                    "Cannot edit the winning source: the setting is not defined in any scope. " +
                    "Choose an override target (Project or Local) instead."),
            EditMode.OverrideAtProject => new ResolvedTarget(ScopeKind.Project, $"{proj}/.claude/settings.json"),
            EditMode.OverrideAtLocal => new ResolvedTarget(ScopeKind.Local, $"{proj}/.claude/settings.local.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown edit mode."),
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/ScopeTarget.cs tests/ClaudeExplorer.Core.Tests/Mutation/ScopeTargetResolverTests.cs
git commit -m "feat(core): scope-target resolution (edit winner vs override at project/local)"
```

---

### Task 3: `ValidationResult` + `SettingsValidator`

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/ValidationResult.cs`
- Create: `src/ClaudeExplorer.Core/Mutation/SettingsValidator.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/SettingsValidatorTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/SettingsValidatorTests.cs`:

```csharp
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SettingsValidatorTests
{
    private static readonly SettingsValidator Validator = new();

    [Fact]
    public void Valid_settings_pass()
    {
        var json = """
        {
          "model": "claude-opus-4-8",
          "outputStyle": "concise",
          "env": { "FOO": "bar" },
          "permissions": { "allow": ["Bash(ls)"], "deny": [], "defaultMode": "ask" },
          "hooks": { "PreToolUse": [] }
        }
        """;

        var result = Validator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Empty_object_is_valid()
    {
        Assert.True(Validator.Validate("{}").IsValid);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        var json = """
        {
          // a comment
          "model": "x",
        }
        """;

        Assert.True(Validator.Validate(json).IsValid);
    }

    [Fact]
    public void Malformed_json_is_invalid()
    {
        var result = Validator.Validate("{ not json ");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void Non_object_root_is_invalid()
    {
        var result = Validator.Validate("[1, 2, 3]");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must be a JSON object"));
    }

    [Fact]
    public void Model_must_be_a_string()
    {
        var result = Validator.Validate("""{ "model": 123 }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"model\""));
    }

    [Fact]
    public void Env_values_must_be_strings()
    {
        var result = Validator.Validate("""{ "env": { "FOO": 5 } }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("env.FOO"));
    }

    [Fact]
    public void Env_must_be_an_object()
    {
        var result = Validator.Validate("""{ "env": "nope" }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"env\""));
    }

    [Fact]
    public void Permission_lists_must_contain_only_strings()
    {
        var result = Validator.Validate("""{ "permissions": { "allow": ["ok", 7] } }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("permissions.allow"));
    }

    [Fact]
    public void Permissions_must_be_an_object()
    {
        var result = Validator.Validate("""{ "permissions": [] }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"permissions\""));
    }

    [Fact]
    public void Hooks_must_be_an_object()
    {
        var result = Validator.Validate("""{ "hooks": [] }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"hooks\""));
    }

    [Fact]
    public void Multiple_errors_are_all_reported()
    {
        var result = Validator.Validate("""{ "model": 1, "outputStyle": 2 }""");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `ValidationResult`, `SettingsValidator` not found.

- [ ] **Step 3: Implement `ValidationResult.cs`**

`src/ClaudeExplorer.Core/Mutation/ValidationResult.cs`:

```csharp
namespace ClaudeExplorer.Core.Mutation;

/// <summary>Outcome of validating proposed file content before it is written.</summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Fail(params string[] errors) => new(false, errors);
}
```

- [ ] **Step 4: Implement `SettingsValidator.cs`**

`src/ClaudeExplorer.Core/Mutation/SettingsValidator.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Structural validator for <c>settings.json</c> content. Not a full JSON-schema engine: it parses
/// the JSON (tolerating comments + trailing commas, exactly like <c>SettingsReader</c>) and checks
/// the shape of the keys this tool understands, so a write can never corrupt a settings file or
/// produce a type the merge engine would silently drop. All problems are collected, not just the
/// first.
/// </summary>
public sealed class SettingsValidator
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ValidationResult Validate(string content)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content, nodeOptions: null, documentOptions: DocOptions);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail($"Invalid JSON: {ex.Message}");
        }

        if (node is not JsonObject root)
            return ValidationResult.Fail("Settings root must be a JSON object.");

        var errors = new List<string>();

        CheckString(root, "model", "model", errors);
        CheckString(root, "outputStyle", "outputStyle", errors);

        if (root.TryGetPropertyValue("env", out var env) && env is not null)
        {
            if (env is not JsonObject envObj)
                errors.Add("\"env\" must be a JSON object.");
            else
                foreach (var kv in envObj)
                    if (!IsString(kv.Value))
                        errors.Add($"\"env.{kv.Key}\" must be a string.");
        }

        if (root.TryGetPropertyValue("permissions", out var perms) && perms is not null)
        {
            if (perms is not JsonObject permObj)
                errors.Add("\"permissions\" must be a JSON object.");
            else
            {
                CheckStringArray(permObj, "allow", "permissions.allow", errors);
                CheckStringArray(permObj, "deny", "permissions.deny", errors);
                CheckStringArray(permObj, "ask", "permissions.ask", errors);
                CheckString(permObj, "defaultMode", "permissions.defaultMode", errors);
            }
        }

        if (root.TryGetPropertyValue("hooks", out var hooks) && hooks is not null && hooks is not JsonObject)
            errors.Add("\"hooks\" must be a JSON object.");

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    private static bool IsString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out _);

    private static void CheckString(JsonObject obj, string key, string label, List<string> errors)
    {
        if (obj.TryGetPropertyValue(key, out var val) && val is not null && !IsString(val))
            errors.Add($"\"{label}\" must be a string.");
    }

    private static void CheckStringArray(JsonObject obj, string key, string label, List<string> errors)
    {
        if (!obj.TryGetPropertyValue(key, out var val) || val is null) return;
        if (val is not JsonArray arr)
        {
            errors.Add($"\"{label}\" must be an array of strings.");
            return;
        }
        foreach (var item in arr)
            if (!IsString(item))
            {
                errors.Add($"\"{label}\" must contain only strings.");
                break;
            }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/ValidationResult.cs src/ClaudeExplorer.Core/Mutation/SettingsValidator.cs tests/ClaudeExplorer.Core.Tests/Mutation/SettingsValidatorTests.cs
git commit -m "feat(core): settings.json structural validator + ValidationResult"
```

---

### Task 4: `FrontmatterValidator`

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/FrontmatterValidator.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/FrontmatterValidatorTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/FrontmatterValidatorTests.cs`:

```csharp
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class FrontmatterValidatorTests
{
    [Fact]
    public void Valid_frontmatter_with_required_fields_passes()
    {
        var doc = "---\nname: my-skill\ndescription: Does a thing\n---\nBody text.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_frontmatter_block_is_invalid()
    {
        var result = new FrontmatterValidator().Validate("Just a body, no frontmatter.");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("frontmatter"));
    }

    [Fact]
    public void Missing_required_field_is_invalid()
    {
        var doc = "---\nname: my-skill\n---\nBody.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("description"));
    }

    [Fact]
    public void Blank_required_value_is_invalid()
    {
        var doc = "---\nname: my-skill\ndescription:   \n---\nBody.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("description"));
    }

    [Fact]
    public void Custom_required_fields_are_enforced()
    {
        var doc = "---\nname: cmd\n---\nBody.";

        var result = new FrontmatterValidator("name").Validate(doc);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Crlf_line_endings_are_handled()
    {
        var doc = "---\r\nname: x\r\ndescription: y\r\n---\r\nBody.";

        Assert.True(new FrontmatterValidator().Validate(doc).IsValid);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `FrontmatterValidator` not found.

- [ ] **Step 3: Implement `FrontmatterValidator.cs`**

`src/ClaudeExplorer.Core/Mutation/FrontmatterValidator.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/FrontmatterValidator.cs tests/ClaudeExplorer.Core.Tests/Mutation/FrontmatterValidatorTests.cs
git commit -m "feat(core): frontmatter validator for markdown artifacts"
```

---

### Task 5: Diff model + `DiffGenerator`

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/Diff.cs`
- Create: `src/ClaudeExplorer.Core/Mutation/DiffGenerator.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/DiffGeneratorTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/DiffGeneratorTests.cs`:

```csharp
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class DiffGeneratorTests
{
    private static readonly DiffGenerator Gen = new();

    [Fact]
    public void Identical_text_has_only_context_lines_and_no_changes()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nb\nc");

        Assert.False(diff.HasChanges);
        Assert.All(diff.Lines, l => Assert.Equal(DiffKind.Context, l.Kind));
        Assert.Equal(3, diff.Lines.Count);
    }

    [Fact]
    public void A_changed_middle_line_is_a_remove_then_add()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nB\nc");

        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.Added);
        Assert.Equal(1, diff.Removed);
        // Order: context a, remove b, add B, context c
        Assert.Collection(diff.Lines,
            l => Assert.Equal((DiffKind.Context, "a"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Removed, "b"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Added, "B"), (l.Kind, l.Text)),
            l => Assert.Equal((DiffKind.Context, "c"), (l.Kind, l.Text)));
    }

    [Fact]
    public void Appended_lines_are_additions_with_null_old_line_numbers()
    {
        var diff = Gen.Generate("a", "a\nb");

        var added = Assert.Single(diff.Lines, l => l.Kind == DiffKind.Added);
        Assert.Equal("b", added.Text);
        Assert.Null(added.OldLine);
        Assert.Equal(2, added.NewLine);
    }

    [Fact]
    public void Removed_lines_are_removals_with_null_new_line_numbers()
    {
        var diff = Gen.Generate("a\nb", "a");

        var removed = Assert.Single(diff.Lines, l => l.Kind == DiffKind.Removed);
        Assert.Equal("b", removed.Text);
        Assert.Equal(2, removed.OldLine);
        Assert.Null(removed.NewLine);
    }

    [Fact]
    public void Context_lines_carry_both_line_numbers()
    {
        var diff = Gen.Generate("a\nb\nc", "a\nB\nc");

        var c = Assert.Single(diff.Lines, l => l.Text == "c" && l.Kind == DiffKind.Context);
        Assert.Equal(3, c.OldLine);
        Assert.Equal(3, c.NewLine);
    }

    [Fact]
    public void Crlf_is_normalized_before_diffing()
    {
        var diff = Gen.Generate("a\r\nb", "a\nb");

        Assert.False(diff.HasChanges);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `DiffKind`, `DiffLine`, `Diff`, `DiffGenerator` not found.

- [ ] **Step 3: Implement `Diff.cs`**

`src/ClaudeExplorer.Core/Mutation/Diff.cs`:

```csharp
namespace ClaudeExplorer.Core.Mutation;

public enum DiffKind { Context, Added, Removed }

/// <summary>
/// One line of a diff. <see cref="OldLine"/> / <see cref="NewLine"/> are 1-based line numbers in
/// the before / after text, or <c>null</c> when the line does not exist on that side.
/// </summary>
public sealed record DiffLine(DiffKind Kind, string Text, int? OldLine, int? NewLine);

public sealed record Diff(IReadOnlyList<DiffLine> Lines)
{
    public bool HasChanges => Lines.Any(l => l.Kind != DiffKind.Context);
    public int Added => Lines.Count(l => l.Kind == DiffKind.Added);
    public int Removed => Lines.Count(l => l.Kind == DiffKind.Removed);
}
```

- [ ] **Step 4: Implement `DiffGenerator.cs`**

`src/ClaudeExplorer.Core/Mutation/DiffGenerator.cs`:

```csharp
namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Produces a line-oriented diff between two text blobs using a longest-common-subsequence
/// backtrace. Deterministic; renders the before / after preview for the safe-edit flow. Line
/// endings are normalized to <c>\n</c> before comparison.
/// </summary>
public sealed class DiffGenerator
{
    public Diff Generate(string before, string after)
    {
        var a = SplitLines(before);
        var b = SplitLines(after);

        // lcs[i, j] = length of the longest common subsequence of a[i..] and b[j..].
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (int i = a.Length - 1; i >= 0; i--)
            for (int j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var lines = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                lines.Add(new DiffLine(DiffKind.Context, a[x], x + 1, y + 1));
                x++; y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                lines.Add(new DiffLine(DiffKind.Removed, a[x], x + 1, null));
                x++;
            }
            else
            {
                lines.Add(new DiffLine(DiffKind.Added, b[y], null, y + 1));
                y++;
            }
        }
        while (x < a.Length) { lines.Add(new DiffLine(DiffKind.Removed, a[x], x + 1, null)); x++; }
        while (y < b.Length) { lines.Add(new DiffLine(DiffKind.Added, b[y], null, y + 1)); y++; }

        return new Diff(lines);
    }

    private static string[] SplitLines(string text)
        => (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/Diff.cs src/ClaudeExplorer.Core/Mutation/DiffGenerator.cs tests/ClaudeExplorer.Core.Tests/Mutation/DiffGeneratorTests.cs
git commit -m "feat(core): LCS line-diff generator for edit previews"
```

---

### Task 6: `BackupEntry` + `IBackupStore` + `FileBackupStore`

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/Backup.cs`
- Create: `src/ClaudeExplorer.Core/Mutation/FileBackupStore.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/FileBackupStoreTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/FileBackupStoreTests.cs`:

```csharp
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class FileBackupStoreTests
{
    private static FileBackupStore NewStore(InMemoryFileSystem fs)
        => new(fs, fs, "/backups");

    [Fact]
    public void Backup_of_existing_file_reads_content_back()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/.claude/settings.json", "{\"model\":\"x\"}");
        var store = NewStore(fs);

        var entry = store.Backup("/p/.claude/settings.json", originalContent: null, originalExisted: true, "2026-06-07T10:00:00Z");

        Assert.True(entry.OriginalExisted);
        Assert.Equal("/p/.claude/settings.json", entry.OriginalPath);
        Assert.StartsWith("/backups/", entry.BackupPath);
        Assert.Equal("{\"model\":\"x\"}", store.Read(entry));
    }

    [Fact]
    public void Backup_uses_provided_content_when_given()
    {
        var fs = new InMemoryFileSystem();
        var store = NewStore(fs);

        var entry = store.Backup("/p/a.json", originalContent: "provided", originalExisted: true, "2026-06-07T10:00:00Z");

        Assert.Equal("provided", store.Read(entry));
    }

    [Fact]
    public void Backup_of_absent_file_stores_no_content_and_Read_throws()
    {
        var fs = new InMemoryFileSystem();
        var store = NewStore(fs);

        var entry = store.Backup("/p/new.json", originalContent: null, originalExisted: false, "2026-06-07T10:00:00Z");

        Assert.False(entry.OriginalExisted);
        Assert.Throws<InvalidOperationException>(() => store.Read(entry));
    }

    [Fact]
    public void Repeated_backups_with_same_timestamp_do_not_collide()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/a.json", "one");
        var store = NewStore(fs);

        var first = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");
        fs.WriteAllText("/p/a.json", "two");
        var second = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");

        Assert.NotEqual(first.BackupPath, second.BackupPath);
        Assert.Equal("one", store.Read(first));
        Assert.Equal("two", store.Read(second));
    }

    [Fact]
    public void Backup_path_sanitizes_timestamp_punctuation()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/a.json", "x");
        var store = NewStore(fs);

        var entry = store.Backup("/p/a.json", null, true, "2026-06-07T10:00:00Z");

        Assert.DoesNotContain(":", entry.BackupPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `BackupEntry`, `IBackupStore`, `FileBackupStore` not found.

- [ ] **Step 3: Implement `Backup.cs`**

`src/ClaudeExplorer.Core/Mutation/Backup.cs`:

```csharp
namespace ClaudeExplorer.Core.Mutation;

/// <summary>A snapshot of a file taken before it was mutated, so the change can be reversed.
/// When <see cref="OriginalExisted"/> is false the file was newly created and undo deletes it.</summary>
public sealed record BackupEntry(
    string OriginalPath,
    string BackupPath,
    string Timestamp,
    bool OriginalExisted);

/// <summary>Stores pre-mutation file snapshots and reads them back for undo.</summary>
public interface IBackupStore
{
    /// <summary>
    /// Snapshot <paramref name="originalPath"/>. Pass <paramref name="originalExisted"/> = false
    /// (with <paramref name="originalContent"/> = null) when the file does not yet exist; undo of
    /// such a change deletes the created file. When the file exists, <paramref name="originalContent"/>
    /// may be supplied to avoid a re-read, or left null to read it from the store's file system.
    /// </summary>
    BackupEntry Backup(string originalPath, string? originalContent, bool originalExisted, string timestamp);

    /// <summary>Read the snapshotted content of a backup. Throws if the original did not exist.</summary>
    string Read(BackupEntry entry);
}
```

- [ ] **Step 4: Implement `FileBackupStore.cs`**

`src/ClaudeExplorer.Core/Mutation/FileBackupStore.cs`:

```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// File-backed <see cref="IBackupStore"/>. Each snapshot is written under
/// <c>{backupRoot}/{sanitizedTimestamp}-{n}-{fileName}.bak</c>, where <c>n</c> is a monotonic
/// counter so repeated backups (even within one timestamp) never collide. Uses the same
/// <see cref="IFileSystem"/> / <see cref="IFileWriter"/> seams as the rest of Core, so it is fully
/// testable against the in-memory fake and never touches the real machine in tests.
/// </summary>
public sealed class FileBackupStore : IBackupStore
{
    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly string _backupRoot;
    private int _counter;

    public FileBackupStore(IFileSystem fs, IFileWriter writer, string backupRoot)
    {
        _fs = fs;
        _writer = writer;
        _backupRoot = backupRoot.Replace('\\', '/').TrimEnd('/');
    }

    public BackupEntry Backup(string originalPath, string? originalContent, bool originalExisted, string timestamp)
    {
        var normalized = originalPath.Replace('\\', '/');

        if (originalExisted)
        {
            var content = originalContent ?? _fs.ReadAllText(normalized);
            var name = normalized.Substring(normalized.LastIndexOf('/') + 1);
            var backupPath = $"{_backupRoot}/{Sanitize(timestamp)}-{++_counter}-{name}.bak";
            _writer.WriteAllText(backupPath, content);
            return new BackupEntry(normalized, backupPath, timestamp, true);
        }

        // Nothing to snapshot; record the absence so undo can delete the created file.
        return new BackupEntry(normalized, "", timestamp, false);
    }

    public string Read(BackupEntry entry)
    {
        if (!entry.OriginalExisted)
            throw new InvalidOperationException(
                $"Backup for {entry.OriginalPath} has no content: the file did not exist when snapshotted.");
        return _fs.ReadAllText(entry.BackupPath);
    }

    private static string Sanitize(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        return new string(chars);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/Backup.cs src/ClaudeExplorer.Core/Mutation/FileBackupStore.cs tests/ClaudeExplorer.Core.Tests/Mutation/FileBackupStoreTests.cs
git commit -m "feat(core): timestamped file backup store for reversible writes"
```

---

### Task 7: scope-aware `ChangeLog`

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/ChangeLog.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/ChangeLogTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/ChangeLogTests.cs`:

```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class ChangeLogTests
{
    private static ChangeLogEntry Entry(ScopeKind scope, string desc) => new(
        Id: "",
        Timestamp: "2026-06-07T10:00:00Z",
        Kind: ChangeKind.Edit,
        Scope: scope,
        FilePath: $"/{scope}/settings.json",
        Description: desc,
        Backup: null,
        UndoCommand: null,
        IsUndone: false);

    [Fact]
    public void Record_assigns_a_sequential_id_when_none_is_given()
    {
        var log = new ChangeLog();

        var first = log.Record(Entry(ScopeKind.Project, "a"));
        var second = log.Record(Entry(ScopeKind.Project, "b"));

        Assert.Equal("chg-1", first.Id);
        Assert.Equal("chg-2", second.Id);
        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public void Record_keeps_a_provided_id()
    {
        var log = new ChangeLog();

        var entry = log.Record(Entry(ScopeKind.Project, "a") with { Id = "custom" });

        Assert.Equal("custom", entry.Id);
    }

    [Fact]
    public void MarkUndone_flips_the_flag_on_the_matching_entry()
    {
        var log = new ChangeLog();
        var entry = log.Record(Entry(ScopeKind.Local, "a"));

        log.MarkUndone(entry.Id);

        Assert.True(log.Entries.Single(e => e.Id == entry.Id).IsUndone);
    }

    [Fact]
    public void ByScope_groups_entries_in_precedence_order()
    {
        var log = new ChangeLog();
        log.Record(Entry(ScopeKind.Local, "l"));
        log.Record(Entry(ScopeKind.User, "u"));
        log.Record(Entry(ScopeKind.Project, "p"));

        var groups = log.ByScope();

        Assert.Equal(new[] { ScopeKind.User, ScopeKind.Project, ScopeKind.Local },
            groups.Select(g => g.Key).ToArray());
    }

    [Fact]
    public void Entries_preserves_insertion_order()
    {
        var log = new ChangeLog();
        log.Record(Entry(ScopeKind.Project, "first"));
        log.Record(Entry(ScopeKind.Project, "second"));

        Assert.Equal(new[] { "first", "second" }, log.Entries.Select(e => e.Description).ToArray());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `ChangeKind`, `ChangeLogEntry`, `ChangeLog` not found.

- [ ] **Step 3: Implement `ChangeLog.cs`**

`src/ClaudeExplorer.Core/Mutation/ChangeLog.cs`:

```csharp
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

public enum ChangeKind { Edit, Install, Uninstall }

/// <summary>
/// One recorded mutation. <see cref="Backup"/> is present for reversible config edits; installs
/// carry an <see cref="UndoCommand"/> (the <c>claude</c> CLI args that reverse them) instead.
/// </summary>
public sealed record ChangeLogEntry(
    string Id,
    string Timestamp,
    ChangeKind Kind,
    ScopeKind Scope,
    string FilePath,
    string Description,
    BackupEntry? Backup,
    IReadOnlyList<string>? UndoCommand,
    bool IsUndone);

/// <summary>
/// In-memory, scope-aware record of every mutation the <see cref="Mutator"/> performs. The UI
/// persists this later; Core only needs it queryable and groupable by scope for review. Insertion
/// order is preserved; <see cref="ByScope"/> groups in precedence order for the change-log screen.
/// </summary>
public sealed class ChangeLog
{
    private readonly List<ChangeLogEntry> _entries = new();
    private int _seq;

    public IReadOnlyList<ChangeLogEntry> Entries => _entries;

    /// <summary>Append an entry. If its <see cref="ChangeLogEntry.Id"/> is empty, a sequential
    /// id (<c>chg-N</c>) is assigned. Returns the stored entry (with its final id).</summary>
    public ChangeLogEntry Record(ChangeLogEntry entry)
    {
        var stored = string.IsNullOrEmpty(entry.Id) ? entry with { Id = $"chg-{++_seq}" } : entry;
        _entries.Add(stored);
        return stored;
    }

    /// <summary>Mark the entry with <paramref name="id"/> as undone (no-op if not found).</summary>
    public void MarkUndone(string id)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Id == id)
            {
                _entries[i] = _entries[i] with { IsUndone = true };
                return;
            }
    }

    /// <summary>Entries grouped by the scope they touched, in precedence order (User→Enterprise).</summary>
    public IReadOnlyList<IGrouping<ScopeKind, ChangeLogEntry>> ByScope()
        => _entries.GroupBy(e => e.Scope).OrderBy(g => (int)g.Key).ToList();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/ChangeLog.cs tests/ClaudeExplorer.Core.Tests/Mutation/ChangeLogTests.cs
git commit -m "feat(core): scope-aware change log with undo tracking"
```

---

### Task 8: `Mutator` — apply edit / install / undo

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/Mutator.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/MutatorTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/MutatorTests.cs`:

```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class MutatorTests
{
    private const string Ts = "2026-06-07T10:00:00Z";

    private static (Mutator mutator, InMemoryFileSystem fs, ChangeLog log, FakeProcessRunner runner) Build(InMemoryFileSystem? seed = null)
    {
        var fs = seed ?? new InMemoryFileSystem();
        var backups = new FileBackupStore(fs, fs, "/backups");
        var log = new ChangeLog();
        var runner = new FakeProcessRunner();
        var mutator = new Mutator(fs, fs, backups, log, runner);
        return (mutator, fs, log, runner);
    }

    private static ResolvedTarget ProjectTarget(string projectDir = "/proj")
        => new(ScopeKind.Project, $"{projectDir}/.claude/settings.json");

    [Fact]
    public void PreviewEdit_of_new_file_reports_no_old_content_and_all_additions()
    {
        var (mutator, _, _, _) = Build();

        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{\n  \"model\": \"x\"\n}");

        Assert.False(preview.TargetExisted);
        Assert.Equal("", preview.OldContent);
        Assert.True(preview.Diff.HasChanges);
        Assert.True(preview.Validation.IsValid);
    }

    [Fact]
    public void ApplyEdit_writes_content_and_records_a_change_entry()
    {
        var (mutator, fs, log, _) = Build();
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"x\" }");

        var entry = mutator.ApplyEdit(preview, Ts);

        Assert.Equal("{ \"model\": \"x\" }", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Equal(ChangeKind.Edit, entry.Kind);
        Assert.Equal(ScopeKind.Project, entry.Scope);
        Assert.Same(entry, Assert.Single(log.Entries));
    }

    [Fact]
    public void ApplyEdit_refuses_invalid_content_and_writes_nothing()
    {
        var (mutator, fs, log, _) = Build();
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": 123 }");

        Assert.Throws<MutationException>(() => mutator.ApplyEdit(preview, Ts));
        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Undo_of_edit_on_preexisting_file_restores_original_content()
    {
        var seed = new InMemoryFileSystem().AddFile("/proj/.claude/settings.json", "{ \"model\": \"old\" }");
        var (mutator, fs, _, _) = Build(seed);
        var preview = mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"new\" }");
        var entry = mutator.ApplyEdit(preview, Ts);
        Assert.Equal("{ \"model\": \"new\" }", fs.ReadAllText("/proj/.claude/settings.json"));

        mutator.Undo(entry);

        Assert.Equal("{ \"model\": \"old\" }", fs.ReadAllText("/proj/.claude/settings.json"));
    }

    [Fact]
    public void Undo_of_edit_that_created_the_file_deletes_it()
    {
        var (mutator, fs, _, _) = Build();
        var entry = mutator.ApplyEdit(mutator.PreviewSettingsEdit(ProjectTarget(), "{ \"model\": \"x\" }"), Ts);
        Assert.True(fs.FileExists("/proj/.claude/settings.json"));

        mutator.Undo(entry);

        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
    }

    [Fact]
    public void Undo_marks_the_entry_undone_and_a_second_undo_throws()
    {
        var (mutator, _, log, _) = Build();
        var entry = mutator.ApplyEdit(mutator.PreviewSettingsEdit(ProjectTarget(), "{}"), Ts);

        mutator.Undo(entry);

        Assert.True(log.Entries.Single().IsUndone);
        Assert.Throws<MutationException>(() => mutator.Undo(entry));
    }

    [Fact]
    public void Install_runs_the_claude_cli_and_records_an_install_entry()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddVersion("claude", "ok"); // exit 0
        var request = new InstallRequest(
            "acme-skill", ScopeKind.User,
            InstallArgs: new[] { "plugin", "install", "acme-skill" },
            UninstallArgs: new[] { "plugin", "uninstall", "acme-skill" });

        var entry = mutator.Install(request, Ts);

        Assert.Equal(ChangeKind.Install, entry.Kind);
        Assert.Equal("acme-skill", entry.FilePath);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("claude", call.Executable);
        Assert.Equal(new[] { "plugin", "install", "acme-skill" }, call.Arguments);
        Assert.Same(entry, Assert.Single(log.Entries));
    }

    [Fact]
    public void Install_throws_when_the_cli_exits_nonzero()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddResult("claude", new ClaudeExplorer.Core.Dependencies.ProcessResult(1, "", "boom"));
        var request = new InstallRequest("bad", ScopeKind.User, new[] { "plugin", "install", "bad" }, new[] { "plugin", "uninstall", "bad" });

        var ex = Assert.Throws<MutationException>(() => mutator.Install(request, Ts));

        Assert.Contains("boom", ex.Message);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Undo_of_install_runs_the_uninstall_command()
    {
        var (mutator, _, log, runner) = Build();
        runner.AddVersion("claude", "ok");
        var request = new InstallRequest("acme", ScopeKind.User, new[] { "plugin", "install", "acme" }, new[] { "plugin", "uninstall", "acme" });
        var entry = mutator.Install(request, Ts);

        mutator.Undo(entry);

        Assert.Equal(2, runner.Invocations.Count);
        Assert.Equal(new[] { "plugin", "uninstall", "acme" }, runner.Invocations[1].Arguments);
        Assert.True(log.Entries.Single().IsUndone);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `Mutator`, `EditPreview`, `InstallRequest`, `MutationException` not found.

- [ ] **Step 3: Implement `Mutator.cs`**

`src/ClaudeExplorer.Core/Mutation/Mutator.cs`:

```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>A previewed config edit: the resolved destination, the before/after content, the diff,
/// and the validation outcome. <see cref="Mutator.ApplyEdit"/> refuses to write unless
/// <see cref="Validation"/> is valid.</summary>
public sealed record EditPreview(
    ResolvedTarget Target,
    string OldContent,
    string NewContent,
    Diff Diff,
    ValidationResult Validation,
    bool TargetExisted);

/// <summary>A request to install a catalog item by delegating to the <c>claude</c> CLI. The
/// uninstall args are captured up front so the change can be undone later.</summary>
public sealed record InstallRequest(
    string ItemName,
    ScopeKind Scope,
    IReadOnlyList<string> InstallArgs,
    IReadOnlyList<string> UninstallArgs);

/// <summary>Raised when a mutation is refused (invalid content) or fails (CLI non-zero exit).</summary>
public sealed class MutationException : Exception
{
    public MutationException(string message) : base(message) { }
}

/// <summary>
/// The single safe-mutation entry point. Config edits are direct file writes guarded by
/// validate → backup → write → record; installs delegate to the <c>claude</c> CLI via
/// <see cref="IProcessRunner"/>. Every applied change is reversible via <see cref="Undo"/>, which
/// restores (or deletes) the backed-up file for edits, or runs the recorded uninstall command for
/// installs.
/// </summary>
public sealed class Mutator
{
    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly IBackupStore _backups;
    private readonly ChangeLog _log;
    private readonly SettingsValidator _settingsValidator;
    private readonly DiffGenerator _diff;
    private readonly IProcessRunner _runner;
    private readonly string _claudeExecutable;

    public Mutator(
        IFileSystem fs,
        IFileWriter writer,
        IBackupStore backups,
        ChangeLog log,
        IProcessRunner runner,
        SettingsValidator? settingsValidator = null,
        string claudeExecutable = "claude")
    {
        _fs = fs;
        _writer = writer;
        _backups = backups;
        _log = log;
        _runner = runner;
        _settingsValidator = settingsValidator ?? new SettingsValidator();
        _diff = new DiffGenerator();
        _claudeExecutable = claudeExecutable;
    }

    /// <summary>Build a preview for replacing <paramref name="target"/>'s content with an explicit
    /// validation result. No write happens.</summary>
    public EditPreview PreviewEdit(ResolvedTarget target, string newContent, ValidationResult validation)
    {
        var existed = _fs.FileExists(target.FilePath);
        var oldContent = existed ? _fs.ReadAllText(target.FilePath) : "";
        return new EditPreview(target, oldContent, newContent, _diff.Generate(oldContent, newContent), validation, existed);
    }

    /// <summary>Preview a settings.json edit, validating the new content with the built-in
    /// <see cref="SettingsValidator"/>.</summary>
    public EditPreview PreviewSettingsEdit(ResolvedTarget target, string newContent)
        => PreviewEdit(target, newContent, _settingsValidator.Validate(newContent));

    /// <summary>Apply a previewed edit: refuse if invalid, back up the current file, write the new
    /// content, and record a reversible change-log entry.</summary>
    public ChangeLogEntry ApplyEdit(EditPreview preview, string timestamp, string? description = null)
    {
        if (!preview.Validation.IsValid)
            throw new MutationException(
                "Refusing to write invalid content: " + string.Join("; ", preview.Validation.Errors));

        var backup = _backups.Backup(
            preview.Target.FilePath,
            preview.TargetExisted ? preview.OldContent : null,
            preview.TargetExisted,
            timestamp);

        _writer.WriteAllText(preview.Target.FilePath, preview.NewContent);

        return _log.Record(new ChangeLogEntry(
            Id: "",
            Timestamp: timestamp,
            Kind: ChangeKind.Edit,
            Scope: preview.Target.Scope,
            FilePath: preview.Target.FilePath,
            Description: description ?? $"Edit {preview.Target.FilePath}",
            Backup: backup,
            UndoCommand: null,
            IsUndone: false));
    }

    /// <summary>Install a catalog item by running the <c>claude</c> CLI. Throws on non-zero exit;
    /// records an install entry carrying the uninstall command for undo.</summary>
    public ChangeLogEntry Install(InstallRequest request, string timestamp)
    {
        var result = _runner.Run(_claudeExecutable, request.InstallArgs);
        if (!result.Success)
            throw new MutationException(
                $"Install of '{request.ItemName}' failed (exit {result.ExitCode}): {result.StdErr}");

        return _log.Record(new ChangeLogEntry(
            Id: "",
            Timestamp: timestamp,
            Kind: ChangeKind.Install,
            Scope: request.Scope,
            FilePath: request.ItemName,
            Description: $"Install {request.ItemName}",
            Backup: null,
            UndoCommand: request.UninstallArgs,
            IsUndone: false));
    }

    /// <summary>Reverse a previously-applied change: restore (or delete) the file for an edit, or
    /// run the recorded uninstall command for an install. Marks the entry undone in the change log.</summary>
    public void Undo(ChangeLogEntry entry)
    {
        if (entry.IsUndone)
            throw new MutationException($"Change '{entry.Id}' has already been undone.");

        switch (entry.Kind)
        {
            case ChangeKind.Edit:
                if (entry.Backup is null)
                    throw new MutationException($"Change '{entry.Id}' has no backup to restore.");
                if (entry.Backup.OriginalExisted)
                    _writer.WriteAllText(entry.Backup.OriginalPath, _backups.Read(entry.Backup));
                else
                    _writer.Delete(entry.Backup.OriginalPath);
                break;

            case ChangeKind.Install:
                if (entry.UndoCommand is null)
                    throw new MutationException($"Change '{entry.Id}' has no uninstall command.");
                var result = _runner.Run(_claudeExecutable, entry.UndoCommand);
                if (!result.Success)
                    throw new MutationException($"Uninstall failed (exit {result.ExitCode}): {result.StdErr}");
                break;

            default:
                throw new MutationException($"Cannot undo change kind {entry.Kind}.");
        }

        _log.MarkUndone(entry.Id);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/Mutator.cs tests/ClaudeExplorer.Core.Tests/Mutation/MutatorTests.cs
git commit -m "feat(core): Mutator — validated edit/install with backup-based undo"
```

---

### Task 9: `SafeMutationService` façade + wiring

**Files:**
- Create: `src/ClaudeExplorer.Core/Mutation/SafeMutationService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Mutation/SafeMutationServiceTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/ClaudeExplorer.Core.Tests/Mutation/SafeMutationServiceTests.cs`:

```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SafeMutationServiceTests
{
    private const string Ts = "2026-06-07T10:00:00Z";

    private static SafeMutationService Build(InMemoryFileSystem fs)
    {
        var backups = new FileBackupStore(fs, fs, "/backups");
        return new SafeMutationService(fs, fs, backups, new FakeProcessRunner());
    }

    [Fact]
    public void End_to_end_override_at_project_resolves_previews_applies_and_undoes()
    {
        var fs = new InMemoryFileSystem();
        var service = Build(fs);

        var preview = service.PreviewSettingsEdit(EditMode.OverrideAtProject, "/proj", winner: null, "{ \"model\": \"x\" }");
        Assert.Equal(ScopeKind.Project, preview.Target.Scope);
        Assert.Equal("/proj/.claude/settings.json", preview.Target.FilePath);
        Assert.True(preview.Validation.IsValid);

        var entry = service.ApplyEdit(preview, Ts);
        Assert.Equal("{ \"model\": \"x\" }", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Single(service.ChangeLog.Entries);

        service.Undo(entry);
        Assert.False(fs.FileExists("/proj/.claude/settings.json"));
        Assert.True(service.ChangeLog.Entries.Single().IsUndone);
    }

    [Fact]
    public void ResolveTarget_edit_winner_follows_provenance()
    {
        var service = Build(new InMemoryFileSystem());
        var winner = new SettingOrigin(ScopeKind.User, "/home/.claude/settings.json", "model");

        var target = service.ResolveTarget(EditMode.EditWinner, "/proj", winner);

        Assert.Equal(ScopeKind.User, target.Scope);
        Assert.Equal("/home/.claude/settings.json", target.FilePath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --nologo`
Expected: FAIL — `SafeMutationService` not found.

- [ ] **Step 3: Implement `SafeMutationService.cs`**

`src/ClaudeExplorer.Core/Mutation/SafeMutationService.cs`:

```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Façade over the safe-mutation layer: resolves where an edit lands (<see cref="ScopeTargetResolver"/>),
/// previews it (diff + validation), applies it with backup + change-log, and supports install /
/// undo. One instance owns the session's <see cref="ChangeLog"/>. This is the single entry point
/// the UI (Phases 7–8) binds to.
/// </summary>
public sealed class SafeMutationService
{
    private readonly ScopeTargetResolver _resolver = new();
    private readonly Mutator _mutator;

    public ChangeLog ChangeLog { get; }

    public SafeMutationService(IFileSystem fs, IFileWriter writer, IBackupStore backups, IProcessRunner runner)
    {
        ChangeLog = new ChangeLog();
        _mutator = new Mutator(fs, writer, backups, ChangeLog, runner);
    }

    public ResolvedTarget ResolveTarget(EditMode mode, string projectDir, SettingOrigin? winner)
        => _resolver.Resolve(mode, projectDir, winner);

    public EditPreview PreviewSettingsEdit(EditMode mode, string projectDir, SettingOrigin? winner, string newContent)
        => _mutator.PreviewSettingsEdit(_resolver.Resolve(mode, projectDir, winner), newContent);

    public ChangeLogEntry ApplyEdit(EditPreview preview, string timestamp, string? description = null)
        => _mutator.ApplyEdit(preview, timestamp, description);

    public ChangeLogEntry Install(InstallRequest request, string timestamp)
        => _mutator.Install(request, timestamp);

    public void Undo(ChangeLogEntry entry) => _mutator.Undo(entry);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --nologo`
Expected: PASS. Full suite green.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Mutation/SafeMutationService.cs tests/ClaudeExplorer.Core.Tests/Mutation/SafeMutationServiceTests.cs
git commit -m "feat(core): SafeMutationService facade wiring resolver + mutator"
```

---

### Task 10: Update roadmap + handoff status

**Files:**
- Modify: `docs/superpowers/plans/2026-06-07-00-roadmap.md`
- Modify: `docs/superpowers/HANDOFF.md`

- [ ] **Step 1: Mark Phase 6 done in the roadmap status table**

In `docs/superpowers/plans/2026-06-07-00-roadmap.md`, change the Phase 6 row from
`| 6 | Safe-mutation | ⏳ Pending | Project "Phase 6" · epic **CLA-30** | — |`
to reflect Done (merged, pushed) with the commit range, and update the "Current test count" and
the detailed-plans paragraph to include `2026-06-07-06-safe-mutation.md`. (Fill the exact commit
hashes after the merge.)

- [ ] **Step 2: Update HANDOFF "Current state" + "Next up"**

In `docs/superpowers/HANDOFF.md`, move Phase 6 into the DONE list with a one-paragraph summary of
the `Mutation/` namespace (IFileWriter seam, ScopeTargetResolver, SettingsValidator +
FrontmatterValidator, DiffGenerator, FileBackupStore, ChangeLog, Mutator, SafeMutationService) and
set "Next up" to **Phase 7 — Blueprint UI shell + Dashboard**.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/plans/2026-06-07-00-roadmap.md docs/superpowers/HANDOFF.md
git commit -m "docs: mark Phase 6 (safe-mutation) done; point next at Phase 7"
```

---

## Self-Review

**Spec coverage (Phase-6 hard contract):**
- Scope-target picker → Task 2 (`ScopeTargetResolver`, `EditMode`). ✅
- Diff preview → Task 5 (`DiffGenerator`/`Diff`) surfaced via `EditPreview.Diff` (Task 8). ✅
- Schema/syntax validation → Task 3 (`SettingsValidator`) + Task 4 (`FrontmatterValidator`);
  enforced at apply time in Task 8. ✅
- Automatic timestamped backups → Task 6 (`FileBackupStore`), taken in `ApplyEdit` (Task 8). ✅
- One-click undo/restore (uninstall for installs) → Task 8 (`Mutator.Undo`). ✅
- Reviewable scope-aware change log → Task 7 (`ChangeLog.ByScope`). ✅
- Edit via direct file write / install via `claude` CLI through `IProcessRunner` → Task 8. ✅
- New write seam (since `IFileSystem` is read-only) → Task 1 (`IFileWriter`). ✅
- Façade for the UI → Task 9 (`SafeMutationService`). ✅

**Placeholder scan:** every code step has complete code; no TODO/TBD; Task 10 is a docs edit and
intentionally defers exact commit hashes (only knowable post-merge). ✅

**Type consistency:** `ResolvedTarget`, `EditMode`, `ValidationResult`, `Diff`/`DiffLine`/`DiffKind`,
`BackupEntry`/`IBackupStore`, `ChangeKind`/`ChangeLogEntry`/`ChangeLog`, `EditPreview`/
`InstallRequest`/`MutationException`/`Mutator`, `SafeMutationService`, `IFileWriter` — names and
signatures are consistent across tasks. `Mutator` ctor matches the `Build` helper in Tests; the
`FakeProcessRunner`/`InMemoryFileSystem` fakes already exist (the latter extended in Task 1).
`ProcessResult` is referenced from `ClaudeExplorer.Core.Dependencies` (Task 8 test uses the fully
qualified name). ✅

**Test isolation:** all tests use `InMemoryFileSystem`/`FakeProcessRunner`; no real machine writes;
backups go to an in-memory `/backups`. ✅

Expected final test count: 149 + 5 (Task 1) + 4 (Task 2) + 12 (Task 3) + 6 (Task 4) + 6 (Task 5) +
5 (Task 6) + 5 (Task 7) + 10 (Task 8) + 2 (Task 9) = **204 passing**.
