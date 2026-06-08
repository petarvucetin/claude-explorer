# Hooks Row Redesign + Inline JSON Editor + Syntax Highlighting — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign Hooks rows so a long matcher breaks into fully-visible tool chips, and clicking a row opens an inline panel with the hook as editable formatted JSON plus the actual referenced script file rendered read-only with syntax highlighting; saves route through the existing safe-mutation flow.

**Architecture:** Pure, tested Core helpers (`HookBlockEditor` to extract/splice one matcher-group within a source `settings.json`; `HookScriptResolver` to locate + language-map the script a command runs). App gets a per-row `HookEditViewModel` (mirrors `SafeEditViewModel`, `new`-ed in the view), an Option-A two-line row, an inline accordion, and a `CodeViewer` upgraded with highlight.js (bundled prebuilt asset + tiny JS interop).

**Tech Stack:** .NET 10, xUnit, Photino.Blazor + MudBlazor, `System.Text.Json.Nodes`, highlight.js 11 (prebuilt).

**Spec:** `docs/superpowers/specs/2026-06-08-hooks-inline-editor-design.md`. Mockups: `ux-explorations/hooks-row-final.html`, `ux-explorations/hooks-inline-panel.html`.

**Conventions (verified):** Tests are xUnit (`[Fact]`/`[Theory]`, underscore names). `InMemoryFileSystem` (in both test projects) implements `IFileSystem` **and** `IFileWriter` and has `AddFile(path, text)`. Build a service with `new SafeMutationService(fs, fs, new FileBackupStore(backupFs, backupFs, "/backups"), new FakeProcessRunner())`. Clock seam is `Func<string>`. `ScopeKind.Plugin` marks plugin-provided hooks; `ScopeKind.Enterprise` marks managed.

Run all tests: `dotnet test ClaudeExplorer.slnx`
Run one project filtered: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~MatcherChipsTests"`

---

### Task 1: `MatcherChips` — split a matcher into display chips

**Files:**
- Modify: `src/ClaudeExplorer.App/Screens/Hooks/HookRows.cs`
- Test: `tests/ClaudeExplorer.App.Tests/Screens/MatcherChipsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ClaudeExplorer.App.Screens.Hooks;

namespace ClaudeExplorer.App.Tests.Screens;

public class MatcherChipsTests
{
    [Fact]
    public void Star_matcher_becomes_a_single_any_chip()
    {
        var chips = HookMatcher.Chips("*");
        var chip = Assert.Single(chips);
        Assert.True(chip.IsAny);
        Assert.Equal("∗ any tool", chip.Text);
    }

    [Fact]
    public void Empty_matcher_is_treated_as_any()
    {
        var chip = Assert.Single(HookMatcher.Chips(""));
        Assert.True(chip.IsAny);
    }

    [Fact]
    public void Pipe_list_splits_into_one_chip_per_tool()
    {
        var chips = HookMatcher.Chips("Bash|Read|Write");
        Assert.Equal(new[] { "Bash", "Read", "Write" }, chips.Select(c => c.Text));
        Assert.All(chips, c => Assert.False(c.IsAny));
    }

    [Fact]
    public void Single_tool_is_one_chip()
        => Assert.Equal("Edit", Assert.Single(HookMatcher.Chips("Edit")).Text);

    [Fact]
    public void Regex_token_is_passed_through_verbatim()
        => Assert.Equal("Notebook.*", Assert.Single(HookMatcher.Chips("Notebook.*")).Text);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~MatcherChipsTests"`
Expected: FAIL — `HookMatcher` does not exist.

- [ ] **Step 3: Write minimal implementation**

Append to `src/ClaudeExplorer.App/Screens/Hooks/HookRows.cs` (below the existing types):

```csharp
/// <summary>One matcher token rendered as a chip. <see cref="IsAny"/> marks the wildcard.</summary>
public sealed record MatcherChip(string Text, bool IsAny);

/// <summary>Splits a hook matcher (a tool-name regex) into display chips. A <c>*</c> or empty matcher
/// is a single "any tool" chip; otherwise the pipe-delimited tokens each become a chip (regex tokens
/// pass through unchanged).</summary>
public static class HookMatcher
{
    public static IReadOnlyList<MatcherChip> Chips(string? matcher)
    {
        if (string.IsNullOrWhiteSpace(matcher) || matcher == "*")
            return new[] { new MatcherChip("∗ any tool", true) };

        return matcher
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => new MatcherChip(t, false))
            .ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~MatcherChipsTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.App/Screens/Hooks/HookRows.cs tests/ClaudeExplorer.App.Tests/Screens/MatcherChipsTests.cs
git commit -m "feat(app): MatcherChips — split hook matcher into display chips"
```

---

### Task 2: `HookRow` carries `SourceGroupIndex`; mapper records it

**Files:**
- Modify: `src/ClaudeExplorer.App/Screens/Hooks/HookRows.cs:11-19` (record), and the mapper loop (`Map`)
- Test: `tests/ClaudeExplorer.App.Tests/Screens/HookRowsTests.cs` (add cases)

- [ ] **Step 1: Write the failing test**

Add to `HookRowsTests.cs`:

```csharp
    [Fact]
    public void Records_source_group_index_per_matcher_group()
    {
        var config = new EffectiveConfig(new[]
        {
            HookSetting("PostToolUse", ScopeKind.User, "/home/.claude/settings.json",
                """
                [
                  { "matcher": "Bash", "hooks": [ { "type": "command", "command": "a.js" } ] },
                  { "matcher": "Edit", "hooks": [ { "type": "command", "command": "b.js" } ] }
                ]
                """),
        });

        var rows = HookRowsMapper.Map(config, Report()).Groups.Single().Rows;

        Assert.Equal(0, rows[0].SourceGroupIndex);
        Assert.Equal(1, rows[1].SourceGroupIndex);
    }

    [Theory]
    [InlineData(ScopeKind.User, true)]
    [InlineData(ScopeKind.Project, true)]
    [InlineData(ScopeKind.Local, true)]
    [InlineData(ScopeKind.Plugin, false)]
    [InlineData(ScopeKind.Enterprise, false)]
    public void IsEditable_only_for_user_project_local(ScopeKind scope, bool expected)
    {
        var config = new EffectiveConfig(new[]
        {
            HookSetting("PreToolUse", scope, "/x/settings.json",
                """[ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "a.js" } ] } ]"""),
        });

        var row = HookRowsMapper.Map(config, Report()).Groups.Single().Rows.Single();
        Assert.Equal(expected, row.IsEditable);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~HookRowsTests"`
Expected: FAIL — `HookRow` has no `SourceGroupIndex` / `IsEditable`.

- [ ] **Step 3: Write minimal implementation**

Replace the `HookRow` record in `HookRows.cs` with:

```csharp
public sealed record HookRow(
    string Event,
    string Matcher,
    string Command,
    string? Type,
    ScopeKind Source,
    string SourceFile,
    string? Runtime,
    HookHealth Health,
    int SourceGroupIndex)
{
    /// <summary>Editable only when the defining source is a writable settings.json scope.
    /// Plugin- and enterprise/managed-provided hooks are read-only.</summary>
    public bool IsEditable => Source is ScopeKind.User or ScopeKind.Project or ScopeKind.Local;
}
```

In `HookRowsMapper.Map`, change the matcher-group loop to track the index and pass it. Replace:

```csharp
                foreach (var groupNode in matcherGroups)
                {
                    if (groupNode is not JsonObject mg) continue;
```

with:

```csharp
                for (var gi = 0; gi < matcherGroups.Count; gi++)
                {
                    if (matcherGroups[gi] is not JsonObject mg) continue;
```

and in the `rows.Add(new HookRow(...))` call append `, SourceGroupIndex: gi)` as the final argument (replacing the closing paren), e.g.:

```csharp
                        rows.Add(new HookRow(
                            evt, matcher!, command, (string?)h["type"],
                            contribution.Origin.Scope, contribution.Origin.FilePath,
                            runtime, HealthOf(command, runtime, health), SourceGroupIndex: gi));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~HookRowsTests"`
Expected: PASS (all existing + 2 new).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.App/Screens/Hooks/HookRows.cs tests/ClaudeExplorer.App.Tests/Screens/HookRowsTests.cs
git commit -m "feat(app): HookRow carries SourceGroupIndex + IsEditable"
```

---

### Task 3: `HookScriptResolver` — locate + language-map the referenced file

**Files:**
- Create: `src/ClaudeExplorer.Core/Hooks/ScriptRef.cs`
- Create: `src/ClaudeExplorer.Core/Hooks/HookScriptResolver.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Hooks/HookScriptResolverTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Hooks;

public class HookScriptResolverTests
{
    private const string Src = "/home/.claude";
    private const string Proj = "/repo";
    private const string User = "/home/.claude";

    [Fact]
    public void Resolves_node_script_relative_to_source_dir()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/home/.claude/hooks/posttool.js", "console.log(1)");

        var r = HookScriptResolver.Resolve(fs, "node hooks/posttool.js", Src, Proj, User);

        Assert.NotNull(r);
        Assert.Equal("/home/.claude/hooks/posttool.js", r!.Path);
        Assert.Equal("javascript", r.Language);
        Assert.True(r.Exists);
    }

    [Fact]
    public void Resolves_python_script_relative_to_project()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/repo/.claude/hooks/guard.py", "print(1)");

        var r = HookScriptResolver.Resolve(fs, "python3 .claude/hooks/guard.py", Src, Proj, User);

        Assert.Equal("python", r!.Language);
        Assert.True(r.Exists);
    }

    [Fact]
    public void Maps_shell_and_powershell_extensions()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/repo/x.sh", "echo hi");
        fs.AddFile("/repo/y.ps1", "Write-Host hi");

        Assert.Equal("bash", HookScriptResolver.Resolve(fs, "bash x.sh", "", Proj, "")!.Language);
        Assert.Equal("powershell", HookScriptResolver.Resolve(fs, "pwsh y.ps1", "", Proj, "")!.Language);
    }

    [Fact]
    public void Templated_plugin_path_is_unresolvable()
        => Assert.Null(HookScriptResolver.Resolve(
            new InMemoryFileSystem(), "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" x", Src, Proj, User));

    [Fact]
    public void Bare_binary_with_no_script_file_returns_null()
        => Assert.Null(HookScriptResolver.Resolve(new InMemoryFileSystem(), "prettier --write", Src, Proj, User));

    [Fact]
    public void Known_extension_not_on_disk_returns_ref_marked_missing()
    {
        var r = HookScriptResolver.Resolve(new InMemoryFileSystem(), "bash scripts/format.sh", "", Proj, "");
        Assert.NotNull(r);
        Assert.False(r!.Exists);
        Assert.Equal("bash", r.Language);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~HookScriptResolverTests"`
Expected: FAIL — `HookScriptResolver` / `ScriptRef` do not exist.

- [ ] **Step 3: Write minimal implementation**

`src/ClaudeExplorer.Core/Hooks/ScriptRef.cs`:

```csharp
namespace ClaudeExplorer.Core.Hooks;

/// <summary>A script file a hook command runs: its resolved absolute path, the highlight.js language
/// id, and whether it currently exists on disk.</summary>
public sealed record ScriptRef(string Path, string Language, bool Exists);
```

`src/ClaudeExplorer.Core/Hooks/HookScriptResolver.cs`:

```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Hooks;

/// <summary>
/// Best-effort resolver for the script file a hook command executes. Strips a known runtime prefix
/// (node/python/bash/…), picks the first script-looking argument, and resolves it against the source
/// file's directory, the project dir, then the user dir. Returns null for inline commands, bare PATH
/// binaries, and unresolved templated paths (e.g. <c>${CLAUDE_PLUGIN_ROOT}</c>).
/// </summary>
public static class HookScriptResolver
{
    private static readonly HashSet<string> Runtimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "deno", "bun", "python", "python3", "py", "uv", "uvx",
        "sh", "bash", "zsh", "pwsh", "powershell", "ruby", "perl", "php",
    };

    private static readonly Dictionary<string, string> LangByExt = new(StringComparer.OrdinalIgnoreCase)
    {
        [".js"] = "javascript", [".mjs"] = "javascript", [".cjs"] = "javascript",
        [".ts"] = "typescript", [".py"] = "python",
        [".sh"] = "bash", [".bash"] = "bash", [".zsh"] = "bash",
        [".ps1"] = "powershell", [".rb"] = "ruby", [".pl"] = "perl", [".php"] = "php",
        [".json"] = "json", [".yml"] = "yaml", [".yaml"] = "yaml",
        [".cmd"] = "dos", [".bat"] = "dos",
    };

    public static ScriptRef? Resolve(IFileSystem fs, string command, string sourceFileDir, string projectDir, string userDir)
    {
        var tokens = Tokenize(command);
        if (tokens.Count == 0) return null;

        var start = Runtimes.Contains(tokens[0]) ? 1 : 0;
        string? candidate = null;
        for (var i = start; i < tokens.Count; i++)
        {
            if (tokens[i].StartsWith('-')) continue;
            candidate = tokens[i];
            break;
        }
        if (candidate is null) return null;
        if (candidate.Contains("${") || candidate.Contains('%')) return null;

        var ext = ExtensionOf(candidate);
        var known = LangByExt.TryGetValue(ext, out var lang);

        foreach (var cand in Candidates(candidate, sourceFileDir, projectDir, userDir))
        {
            if (fs.FileExists(cand))
                return new ScriptRef(Norm(cand), known ? lang! : "plaintext", true);
        }

        if (!known) return null; // inline command / bare binary, not a script file
        return new ScriptRef(Norm(Candidates(candidate, sourceFileDir, projectDir, userDir).First()), lang!, false);
    }

    private static List<string> Tokenize(string command) =>
        command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(t => t.Trim('"', '\'')).ToList();

    private static string ExtensionOf(string path)
    {
        var slash = path.LastIndexOfAny(new[] { '/', '\\' });
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[dot..] : "";
    }

    private static IEnumerable<string> Candidates(string token, string srcDir, string projDir, string userDir)
    {
        var p = token.Replace('\\', '/');
        if (p.StartsWith('/') || (p.Length > 1 && p[1] == ':')) { yield return p; yield break; }
        if (!string.IsNullOrEmpty(srcDir)) yield return Combine(srcDir, p);
        if (!string.IsNullOrEmpty(projDir)) yield return Combine(projDir, p);
        if (!string.IsNullOrEmpty(userDir)) yield return Combine(userDir, p);
    }

    private static string Combine(string a, string b) => $"{a.Replace('\\', '/').TrimEnd('/')}/{b}";
    private static string Norm(string p) => p.Replace('\\', '/');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~HookScriptResolverTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Hooks/ tests/ClaudeExplorer.Core.Tests/Hooks/HookScriptResolverTests.cs
git commit -m "feat(core): HookScriptResolver — locate + language-map a hook's script file"
```

---

### Task 4: `HookBlockEditor` — extract + splice one matcher-group

**Files:**
- Create: `src/ClaudeExplorer.Core/Hooks/HookBlockEditor.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Hooks/HookBlockEditorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Hooks;

public class HookBlockEditorTests
{
    private const string Source = """
        {
          "model": "opus",
          "hooks": {
            "PostToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "command", "command": "a.js" } ] },
              { "matcher": "Edit", "hooks": [ { "type": "command", "command": "b.js" } ] }
            ]
          }
        }
        """;

    [Fact]
    public void Extract_returns_the_indexed_block_pretty_printed()
    {
        var block = HookBlockEditor.ExtractBlock(Source, "PostToolUse", 1);
        Assert.Contains("\"matcher\": \"Edit\"", block);
        Assert.Contains("\n", block); // pretty-printed
        Assert.DoesNotContain("Bash", block);
    }

    [Fact]
    public void Extract_throws_on_index_out_of_range()
        => Assert.Throws<MutationException>(() => HookBlockEditor.ExtractBlock(Source, "PostToolUse", 5));

    [Fact]
    public void Splice_replaces_only_the_target_block_and_preserves_the_rest()
    {
        var edited = """{ "matcher": "Edit|Write", "hooks": [ { "type": "command", "command": "b2.js" } ] }""";

        var result = HookBlockEditor.SpliceBlock(Source, "PostToolUse", 1, edited);

        Assert.Contains("b2.js", result);
        Assert.Contains("Edit|Write", result);
        Assert.Contains("\"model\": \"opus\"", result); // sibling preserved
        Assert.Contains("a.js", result);                // other block preserved
        Assert.DoesNotContain("\"b.js\"", result);      // old value gone
    }

    [Fact]
    public void Splice_rejects_invalid_json()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 0, "{ not json"));

    [Fact]
    public void Splice_rejects_block_without_hooks_array()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 0, """{ "matcher": "Bash" }"""));

    [Fact]
    public void Splice_throws_on_index_out_of_range()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 9, """{ "hooks": [] }"""));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~HookBlockEditorTests"`
Expected: FAIL — `HookBlockEditor` does not exist.

- [ ] **Step 3: Write minimal implementation**

`src/ClaudeExplorer.Core/Hooks/HookBlockEditor.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Hooks;

/// <summary>
/// Extracts and replaces a single matcher-group within a source <c>settings.json</c>'s
/// <c>hooks.&lt;event&gt;</c> array, operating on the raw on-disk text. The user edits one block; the
/// whole file is re-serialized (2-space pretty) so the existing safe-mutation diff/backup/undo operate
/// on the real file. Refusals throw <see cref="MutationException"/>.
/// </summary>
public static class HookBlockEditor
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true, IndentSize = 2, IndentCharacter = ' ',
    };

    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    public static string ExtractBlock(string sourceText, string evt, int sourceGroupIndex)
    {
        var arr = HookArray(sourceText, evt);
        if (arr is null || sourceGroupIndex < 0 || sourceGroupIndex >= arr.Count)
            throw new MutationException($"Hook block hooks.{evt}[{sourceGroupIndex}] not found.");
        return arr[sourceGroupIndex]!.ToJsonString(Pretty);
    }

    public static string SpliceBlock(string sourceText, string evt, int sourceGroupIndex, string editedBlockJson)
    {
        JsonNode? edited;
        try { edited = JsonNode.Parse(editedBlockJson, documentOptions: Lenient); }
        catch (JsonException ex) { throw new MutationException("Edited hook is not valid JSON: " + ex.Message); }

        if (edited is not JsonObject obj || obj["hooks"] is not JsonArray)
            throw new MutationException("Edited hook must be a JSON object with a \"hooks\" array.");

        if (JsonNode.Parse(sourceText, documentOptions: Lenient) is not JsonObject root)
            throw new MutationException("Source settings is not a JSON object.");

        if ((root["hooks"] as JsonObject)?[evt] is not JsonArray arr)
            throw new MutationException($"Source has no hooks.{evt} array.");

        if (sourceGroupIndex < 0 || sourceGroupIndex >= arr.Count)
            throw new MutationException($"Hook block index {sourceGroupIndex} is out of range.");

        arr[sourceGroupIndex] = edited;
        return root.ToJsonString(Pretty);
    }

    private static JsonArray? HookArray(string sourceText, string evt)
    {
        var root = JsonNode.Parse(sourceText, documentOptions: Lenient) as JsonObject;
        return (root?["hooks"] as JsonObject)?[evt] as JsonArray;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~HookBlockEditorTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.Core/Hooks/HookBlockEditor.cs tests/ClaudeExplorer.Core.Tests/Hooks/HookBlockEditorTests.cs
git commit -m "feat(core): HookBlockEditor — extract/splice one matcher-group in settings.json"
```

---

### Task 5: `HookEditViewModel` — per-row edit flow over safe-mutation

**Files:**
- Create: `src/ClaudeExplorer.App/Screens/Hooks/HookEditViewModel.cs`
- Test: `tests/ClaudeExplorer.App.Tests/Screens/HookEditViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ClaudeExplorer.App.Screens.Hooks;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Screens;

public class HookEditViewModelTests
{
    private const string File = "/home/.claude/settings.json";
    private const string Source = """
        {
          "hooks": {
            "PostToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "command", "command": "old.js" } ] }
            ]
          }
        }
        """;

    private static (SafeMutationService svc, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile(File, Source);
        var backup = new InMemoryFileSystem();
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(backup, backup, "/backups"), new FakeProcessRunner());
        return (svc, fs);
    }

    private static HookRow Row(ScopeKind scope = ScopeKind.User) =>
        new("PostToolUse", "Bash", "old.js", "command", scope, File, "node", HookHealth.Ok, SourceGroupIndex: 0);

    private static HookEditViewModel Vm(SafeMutationService svc, InMemoryFileSystem fs, HookRow row) =>
        new(svc, fs, row, () => "2026-06-08T00:00:00Z", "/repo");

    [Fact]
    public void Load_extracts_the_block_text()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        Assert.Contains("old.js", vm.BlockText);
        Assert.Contains("\"matcher\": \"Bash\"", vm.BlockText);
    }

    [Fact]
    public void Save_writes_spliced_file_and_records_change_log()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = """{ "matcher": "Bash", "hooks": [ { "type": "command", "command": "new.js" } ] }""";

        vm.DoPreview();
        vm.Save();

        Assert.NotNull(vm.Applied);
        Assert.Null(vm.Error);
        Assert.Contains("new.js", fs.ReadAllText(File));
        Assert.Single(svc.ChangeLog.Entries);
    }

    [Fact]
    public void Undo_reverts_to_original()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = """{ "matcher": "Bash", "hooks": [ { "type": "command", "command": "new.js" } ] }""";
        vm.DoPreview();
        vm.Save();

        vm.Undo();

        Assert.True(vm.Applied!.IsUndone);
        Assert.Contains("old.js", fs.ReadAllText(File));
    }

    [Fact]
    public void Read_only_row_refuses_to_save()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row(ScopeKind.Plugin));
        Assert.False(vm.IsEditable);

        vm.BlockText = """{ "matcher": "Bash", "hooks": [] }""";
        vm.Save();

        Assert.Null(vm.Applied);
        Assert.NotNull(vm.Error);
        Assert.Contains("old.js", fs.ReadAllText(File)); // unchanged
    }

    [Fact]
    public void Invalid_json_surfaces_error_on_preview()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = "{ not json";

        vm.DoPreview();

        Assert.NotNull(vm.Error);
        Assert.Null(vm.Preview);
    }

    [Theory]
    [InlineData(ScopeKind.User, true)]
    [InlineData(ScopeKind.Project, false)]
    public void IsGlobalEdit_true_for_non_project_scopes(ScopeKind scope, bool expected)
    {
        var (svc, fs) = Build();
        Assert.Equal(expected, Vm(svc, fs, Row(scope)).IsGlobalEdit);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~HookEditViewModelTests"`
Expected: FAIL — `HookEditViewModel` does not exist.

- [ ] **Step 3: Write minimal implementation**

`src/ClaudeExplorer.App/Screens/Hooks/HookEditViewModel.cs`:

```csharp
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Screens.Hooks;

/// <summary>
/// Drives the inline edit of a single hook's matcher-group. Loads the block from the source file,
/// previews/saves by splicing it back into the whole file and routing through
/// <see cref="SafeMutationService"/> (diff → backup → validate → change-log → undo). Read-only when the
/// row's source is plugin/enterprise. Mirrors <c>SafeEditViewModel</c>; <paramref name="nowIso"/> is the
/// injectable clock seam.
/// </summary>
public sealed class HookEditViewModel : ObservableObject
{
    private readonly SafeMutationService _svc;
    private readonly IFileSystem _fs;
    private readonly HookRow _row;
    private readonly Func<string> _nowIso;
    private readonly string _projectDir;

    private string _blockText = "";
    private EditPreview? _preview;
    private ChangeLogEntry? _applied;
    private string? _error;

    public HookEditViewModel(SafeMutationService svc, IFileSystem fs, HookRow row, Func<string> nowIso, string projectDir)
    {
        _svc = svc;
        _fs = fs;
        _row = row;
        _nowIso = nowIso;
        _projectDir = projectDir;
        Load();
    }

    public HookRow Row => _row;
    public bool IsEditable => _row.IsEditable;

    /// <summary>True when the editable source is not project-specific (User/Enterprise/Plugin) — the
    /// "affects every project" warning.</summary>
    public bool IsGlobalEdit => _row.Source is not (ScopeKind.Project or ScopeKind.Local);

    public string BlockText { get => _blockText; set => SetProperty(ref _blockText, value); }
    public EditPreview? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public ChangeLogEntry? Applied { get => _applied; private set => SetProperty(ref _applied, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    private void Load()
    {
        try { BlockText = HookBlockEditor.ExtractBlock(ReadSource(), _row.Event, _row.SourceGroupIndex); }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void DoPreview()
    {
        Error = null;
        try
        {
            var newWhole = HookBlockEditor.SpliceBlock(ReadSource(), _row.Event, _row.SourceGroupIndex, BlockText);
            var winner = new SettingOrigin(_row.Source, _row.SourceFile, $"hooks.{_row.Event}");
            Preview = _svc.PreviewSettingsEdit(EditMode.EditWinner, _projectDir, winner, newWhole);
        }
        catch (Exception ex) { Error = ex.Message; Preview = null; }
    }

    public void Save()
    {
        if (!IsEditable) { Error = "This hook is read-only (plugin/managed source)."; return; }
        if (Preview is null) DoPreview();
        if (Preview is null) return;
        if (!Preview.Validation.IsValid) { Error = string.Join("; ", Preview.Validation.Errors); return; }
        try
        {
            Applied = _svc.ApplyEdit(Preview, _nowIso(), $"Edit {_row.Event} hook ({Summarize(_row.Matcher)})");
            Error = null;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void Undo()
    {
        if (Applied is null) return;
        try { _svc.Undo(Applied); Applied = Applied with { IsUndone = true }; Error = null; }
        catch (Exception ex) { Error = ex.Message; }
    }

    private string ReadSource() => _fs.FileExists(_row.SourceFile) ? _fs.ReadAllText(_row.SourceFile) : "";

    private static string Summarize(string matcher) => matcher.Length <= 24 ? matcher : matcher[..24] + "…";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~HookEditViewModelTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeExplorer.App/Screens/Hooks/HookEditViewModel.cs tests/ClaudeExplorer.App.Tests/Screens/HookEditViewModelTests.cs
git commit -m "feat(app): HookEditViewModel — inline per-hook edit over safe-mutation"
```

---

### Task 6: highlight.js asset + JS interop + `CodeViewer` `Language`/scroll

**Files:**
- Create: `src/ClaudeExplorer.App/wwwroot/lib/highlight/highlight.min.js` (downloaded)
- Create: `src/ClaudeExplorer.App/wwwroot/lib/highlight/highlightjs-line-numbers.min.js` (downloaded)
- Create: `src/ClaudeExplorer.App/wwwroot/lib/highlight/theme.css` (downloaded)
- Create: `src/ClaudeExplorer.App/wwwroot/js/codeview.js`
- Modify: `src/ClaudeExplorer.App/wwwroot/index.html:8-23`
- Modify: `src/ClaudeExplorer.App/Components/CodeViewer.razor`
- Modify: `src/ClaudeExplorer.App/wwwroot/css/blueprint.css` (capped-height rule)

No unit test (JS interop in Photino is headless-unverifiable). Verify by build + `/run`.

- [ ] **Step 1: Download the prebuilt assets (11.10.0 "common" bundle covers json/js/ts/python/bash/powershell/yaml)**

Run (PowerShell):

```powershell
$dir = "src/ClaudeExplorer.App/wwwroot/lib/highlight"
New-Item -ItemType Directory -Force $dir | Out-Null
Invoke-WebRequest "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.10.0/highlight.min.js" -OutFile "$dir/highlight.min.js"
Invoke-WebRequest "https://cdnjs.cloudflare.com/ajax/libs/highlightjs-line-numbers.js/2.8.0/highlightjs-line-numbers.min.js" -OutFile "$dir/highlightjs-line-numbers.min.js"
Invoke-WebRequest "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.10.0/styles/atom-one-dark.min.css" -OutFile "$dir/theme.css"
```

Expected: three files present, non-empty.

- [ ] **Step 2: Add the interop shim** — `src/ClaudeExplorer.App/wwwroot/js/codeview.js`:

```javascript
// Minimal highlight.js bridge. cx.highlight(el) colorizes a <code> block and adds a line-number
// gutter. Read-only views only; safe to call repeatedly (guarded by a data flag).
window.cx = window.cx || {};
window.cx.highlight = function (el) {
    if (!el || !window.hljs) return;
    if (el.dataset.cxHighlighted === el.textContent.length.toString()) return;
    delete el.dataset.highlighted;          // allow re-highlight after content change
    window.hljs.highlightElement(el);
    if (window.hljs.lineNumbersBlock) window.hljs.lineNumbersBlock(el, { singleLine: true });
    el.dataset.cxHighlighted = el.textContent.length.toString();
};
```

- [ ] **Step 3: Reference assets in `index.html`** — add inside `<head>` after `blueprint.css`:

```html
    <link href="lib/highlight/theme.css" rel="stylesheet" />
```

and add before the closing `</body>`, after the MudBlazor script:

```html
    <script src="lib/highlight/highlight.min.js"></script>
    <script src="lib/highlight/highlightjs-line-numbers.min.js"></script>
    <script src="js/codeview.js"></script>
```

- [ ] **Step 4: Upgrade `CodeViewer.razor`** — replace the whole file:

```razor
@inject IJSRuntime JS

<div class="codeview @(Capped ? "capped" : "")">
    <div class="codeview-head">@Title</div>
    @if (Language is null)
    {
        <pre class="codeview-body"><code>@for (var i = 0; i < _lines.Length; i++)
{<span class="ln">@(i + 1)</span>@_lines[i]
}</code></pre>
    }
    else
    {
        <pre class="codeview-body"><code @ref="_codeEl" class="@($"language-{Language}")">@Content</code></pre>
    }
</div>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Content { get; set; } = "";
    /// <summary>highlight.js language id (e.g. "javascript", "json"). Null = plain text with JSON
    /// pretty-print and a manual line gutter (legacy behavior).</summary>
    [Parameter] public string? Language { get; set; }
    /// <summary>Cap the body height and scroll vertically when content overflows.</summary>
    [Parameter] public bool Capped { get; set; }

    private string[] _lines = Array.Empty<string>();
    private ElementReference _codeEl;
    private string? _highlighted;

    protected override void OnParametersSet()
        => _lines = Language is null
            ? ClaudeExplorer.App.Util.JsonFormat.TryPretty(Content).Replace("\r\n", "\n").Split('\n')
            : Array.Empty<string>();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Language is not null && _highlighted != Content)
        {
            await JS.InvokeVoidAsync("cx.highlight", _codeEl);
            _highlighted = Content;
        }
    }
}
```

- [ ] **Step 5: Add the capped-height CSS** — in `blueprint.css`, just after the `.codeview-body` rules (~line 535):

```css
.codeview.capped .codeview-body { max-height: 340px; overflow: auto; }
```

- [ ] **Step 6: Build and verify nothing broke**

Run: `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 7: Manual check (Photino can't be unit-tested)** — `/run` the app, open any screen with a source preview (e.g. Skills), confirm existing JSON/plain views still render (Language defaults to null → unchanged). Highlighting is exercised in Task 7.

- [ ] **Step 8: Commit**

```bash
git add src/ClaudeExplorer.App/wwwroot/lib/highlight/ src/ClaudeExplorer.App/wwwroot/js/codeview.js src/ClaudeExplorer.App/wwwroot/index.html src/ClaudeExplorer.App/Components/CodeViewer.razor src/ClaudeExplorer.App/wwwroot/css/blueprint.css
git commit -m "feat(app): bundle highlight.js + CodeViewer Language/Capped (read-only highlighting)"
```

---

### Task 7: Hooks row redesign + inline accordion editor

**Files:**
- Modify: `src/ClaudeExplorer.App/Pages/Hooks.razor` (rows + inline panel; replace lines 31-66)
- Modify: `src/ClaudeExplorer.App/wwwroot/css/blueprint.css` (hook row + accordion styles)

No unit test (Razor render). VM logic is already covered by Task 5. Verify by build + `/run`.

- [ ] **Step 1: Replace the row loop + detail block in `Hooks.razor`**

Replace lines 31-66 (the `@foreach (var group ...)` block through the closing of the `Vm.Selected` detail panel) with:

```razor
    @{ var gi = 0; }
    @foreach (var group in view.Groups)
    {
        var groupIndex = gi++;
        <div class="grp-label"><span class="name">@group.Event</span><span class="cnt">@group.Rows.Count</span></div>
        @{ var ri = 0; }
        @foreach (var row in group.Rows)
        {
            var r = row;
            var rowKey = (groupIndex, ri);
            var selected = Vm.SelectedKey == rowKey;
            <div class="hookrow @(selected ? "sel" : "")" @onclick="() => Toggle(rowKey, r)">
                <div class="l1">
                    <div class="chips">
                        @foreach (var chip in HookMatcher.Chips(row.Matcher))
                        {
                            <span class="tchip @(chip.IsAny ? "any" : "")">@chip.Text</span>
                        }
                    </div>
                    <div class="right">
                        <ScopeTag Scope="row.Source" />
                        @if (!row.IsEditable) { <span class="ro-tag">read-only</span> }
                        <span class="pill @HookRowsMapper.Pill(row.Health)">@HookRowsMapper.HealthText(row)</span>
                    </div>
                </div>
                <div class="l2"><span class="typ">@(row.Type ?? "command")</span><span class="cmd">@row.Command</span></div>
            </div>
            @if (selected && _editor is not null)
            {
                <div class="hookpanel">
                    @if (_editor.IsEditable && _editor.IsGlobalEdit)
                    {
                        <div class="ed-warn">⚠ Global (@row.Source) source — changes affect every project.</div>
                    }
                    else if (!_editor.IsEditable)
                    {
                        <div class="ed-warn ro">This hook is provided by @row.Source and is read-only.</div>
                    }

                    <div class="seg-head"><span class="t">Hook</span><span class="lang">JSON@(_editor.IsEditable ? " · editable" : " · read-only")</span><span class="path">@row.SourceFile</span></div>
                    @if (_editor.IsEditable)
                    {
                        <textarea class="json-edit" spellcheck="false" @bind="_editor.BlockText" @bind:event="oninput"></textarea>
                    }
                    else
                    {
                        <CodeViewer Title="" Content="@_editor.BlockText" Language="json" Capped="true" />
                    }

                    @{ var script = ResolveScript(row); }
                    @if (script is not null && script.Exists)
                    {
                        <div class="seg-head"><span class="t">Runs file</span><span class="lang ro">@script.Language · read-only</span><span class="path">@script.Path</span></div>
                        <CodeViewer Title="" Content="@Fs.ReadAllText(script.Path)" Language="@script.Language" Capped="true" />
                    }
                    else
                    {
                        <div class="ed-note">@(script is null ? "Runs an inline command — no script file to display." : $"Script not found on disk: {script.Path}")</div>
                    }

                    @if (_editor.Error is { } err) { <div class="ed-err">@err</div> }
                    @if (_editor.Applied is { } applied)
                    {
                        <div class="ed-bar"><span class="log ok">✓ Saved · logged to Change Log (@applied.Scope)</span>
                            <button class="btn ghost" @onclick="() => _editor.Undo()" disabled="@applied.IsUndone">Undo</button></div>
                    }
                    else if (_editor.IsEditable)
                    {
                        <div class="ed-bar">
                            <span class="log">↻ Save logs to Change Log · timestamped backup · one-click undo</span>
                            <button class="btn ghost" @onclick="() => _editor!.DoPreview()">Preview diff</button>
                            <button class="btn ghost" @onclick="() => Toggle((groupIndex, ri), r)">Cancel</button>
                            <button class="btn primary" @onclick="() => _editor!.Save()">Save</button>
                        </div>
                        @if (_editor.Preview is { } pv && pv.Diff.HasChanges)
                        {
                            <CodeViewer Title="diff preview" Content="@DiffText(pv.Diff)" Capped="true" />
                        }
                    }
                </div>
            }
            ri++;
        }
    }
```

- [ ] **Step 2: Add the `@code` members** — in `Hooks.razor`, add these to the existing `@code { }` block (alongside `OnInitialized`/`Dispose`), and add the injects at the top:

At the top of the file (after the existing `@inject` lines):

```razor
@inject ClaudeExplorer.Core.Mutation.SafeMutationService Mutation
@inject ClaudeExplorer.App.Services.IWorkspaceContext Workspace
@inject Func<string> NowIso
```

Inside `@code { }`:

```csharp
    private (int, int)? _selectedKey;
    private HookEditViewModel? _editor;

    public (int, int)? SelectedKey => _selectedKey;

    private void Toggle((int, int) key, HookRow row)
    {
        if (_selectedKey == key) { _selectedKey = null; _editor = null; return; }
        _selectedKey = key;
        _editor = new HookEditViewModel(Mutation, Fs, row, NowIso, Workspace.ProjectDir ?? "");
    }

    private ClaudeExplorer.Core.Hooks.ScriptRef? ResolveScript(HookRow row)
    {
        var dir = row.SourceFile.Replace('\\', '/');
        var slash = dir.LastIndexOf('/');
        var srcDir = slash >= 0 ? dir[..slash] : "";
        return ClaudeExplorer.Core.Hooks.HookScriptResolver.Resolve(
            Fs, row.Command, srcDir, Workspace.ProjectDir ?? "", Workspace.UserDir ?? "");
    }

    private static string DiffText(ClaudeExplorer.Core.Mutation.Diff diff)
        => string.Join("\n", diff.Lines.Select(l => l.Kind switch
        {
            ClaudeExplorer.Core.Mutation.DiffKind.Added => "+ " + l.Text,
            ClaudeExplorer.Core.Mutation.DiffKind.Removed => "- " + l.Text,
            _ => "  " + l.Text,
        }));
```

> Note: `Vm.SelectedKey` in the markup refers to this component's `SelectedKey` (the page owns selection now). The old `Vm.Selected` property on `HooksViewModel` is no longer used by the view; leave it in place (harmless) or remove it in a follow-up. Confirm `IWorkspaceContext` exposes `ProjectDir` and `UserDir` (used elsewhere in `HooksViewModel.Load`).

- [ ] **Step 3: Add hook-row + accordion CSS** — append to `blueprint.css`:

```css
/* Hooks — two-line row (Option A: all matcher chips visible) */
.hookrow { border: 1.5px solid var(--edge); border-radius: 8px; background: var(--panel); padding: 11px 13px; margin-bottom: 8px; cursor: pointer; }
.hookrow:hover { border-color: var(--edge-2); }
.hookrow.sel { border-color: var(--edge-2); border-radius: 8px 8px 0 0; box-shadow: 3px 3px 0 var(--blue-wash); margin-bottom: 0; }
.hookrow .l1 { display: flex; align-items: flex-start; gap: 12px; }
.hookrow .chips { display: flex; flex-wrap: wrap; gap: 5px; flex: 1; }
.hookrow .right { display: flex; align-items: center; gap: 7px; flex: none; }
.hookrow .l2 { display: flex; align-items: center; gap: 8px; margin-top: 10px; padding-top: 9px; border-top: 1px dashed var(--grid-bold); }
.hookrow .cmd { font-family: "Spline Sans Mono", monospace; font-size: 12px; color: var(--ink-soft); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; }
.hookrow .typ { font-family: "Spline Sans Mono", monospace; font-size: 9px; font-weight: 700; letter-spacing: .06em; color: var(--ink-faint); text-transform: uppercase; border: 1px solid var(--edge); border-radius: 4px; padding: 1px 6px; }
.tchip { font-family: "Spline Sans Mono", monospace; font-size: 10px; padding: 2px 7px; border: 1px solid var(--edge); border-radius: 5px; background: var(--paper); color: var(--ink); white-space: nowrap; }
.tchip.any { border-color: var(--blue); color: var(--blue); background: var(--blue-wash); font-weight: 700; }
.ro-tag { font-family: "Spline Sans Mono", monospace; font-size: 8.5px; font-weight: 700; letter-spacing: .06em; color: var(--ink-faint); border: 1px solid var(--edge); border-radius: 3px; padding: 1px 5px; }

/* Hooks — inline accordion panel */
.hookpanel { border: 1.5px solid var(--edge-2); border-top: none; border-radius: 0 0 8px 8px; background: var(--panel); margin: 0 0 14px; padding: 13px; box-shadow: 3px 4px 0 var(--blue-wash); }
.hookpanel .seg-head { display: flex; align-items: center; gap: 9px; margin: 10px 0 6px; }
.hookpanel .seg-head:first-of-type { margin-top: 0; }
.hookpanel .seg-head .t { font-family: "Spline Sans Mono", monospace; font-size: 10px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: var(--ink); }
.hookpanel .seg-head .lang { font-family: "Spline Sans Mono", monospace; font-size: 9px; font-weight: 700; color: #fff; background: var(--blue); border-radius: 3px; padding: 1px 6px; }
.hookpanel .seg-head .lang.ro { background: var(--ink-faint); }
.hookpanel .seg-head .path { font-family: "Spline Sans Mono", monospace; font-size: 10px; color: var(--ink-soft); margin-left: auto; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 50%; }
.json-edit { width: 100%; max-height: 340px; overflow: auto; resize: vertical; background: #0F1722; color: #D7E0EC; border: 1.5px solid var(--edge-2); border-radius: 6px; font-family: "Spline Sans Mono", monospace; font-size: 12px; line-height: 1.55; padding: 11px 13px; box-sizing: border-box; }
.ed-warn { font-family: "Spline Sans Mono", monospace; font-size: 10.5px; color: var(--amber); background: var(--amber-wash); border: 1px solid var(--amber); border-radius: 5px; padding: 6px 9px; margin-bottom: 12px; }
.ed-warn.ro { color: var(--ink-soft); background: var(--paper); border-color: var(--edge); }
.ed-note { font-family: "Spline Sans Mono", monospace; font-size: 11px; color: var(--ink-faint); padding: 8px 0; }
.ed-err { font-family: "Spline Sans Mono", monospace; font-size: 11px; color: var(--red); background: var(--red-wash); border: 1px solid var(--red); border-radius: 5px; padding: 6px 9px; margin-top: 10px; }
.ed-bar { display: flex; align-items: center; gap: 9px; margin-top: 11px; }
.ed-bar .log { font-family: "Spline Sans Mono", monospace; font-size: 10.5px; color: var(--ink-soft); margin-right: auto; }
.ed-bar .log.ok { color: var(--green); }
.hookpanel .btn { font-family: "Spline Sans Mono", monospace; font-size: 11px; font-weight: 700; padding: 6px 13px; border-radius: 6px; border: 1.5px solid var(--edge-2); background: var(--panel); color: var(--ink); cursor: pointer; }
.hookpanel .btn.primary { background: var(--blue); border-color: var(--blue); color: #fff; box-shadow: 2px 2px 0 var(--blue-wash); }
.hookpanel .btn.ghost { border-color: var(--edge); color: var(--ink-soft); }
```

- [ ] **Step 4: Register the clock seam injection** — confirm `Func<string>` is registered in `Program.cs` (it is, line ~87). No DI change needed: `SafeMutationService`, `IFileSystem`, `IWorkspaceContext`, `Func<string>` are all resolvable.

- [ ] **Step 5: Build**

Run: `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary`
Expected: `Build succeeded. 0 Error(s)`. If `IWorkspaceContext` lacks `ProjectDir`/`UserDir`, use the property names from `HooksViewModel.Load` (it calls `_workspace.UserDir`, `_workspace.ProjectDir`).

- [ ] **Step 6: Manual verify** — `/run`, open Hooks:
  - Long matcher renders as wrapping chips; `*` → `∗ any tool`; plugin rows show `read-only`.
  - Click a row → panel opens beneath it; JSON box is pretty-printed; the referenced `.js/.py/.sh` file shows highlighted with line numbers; both boxes scroll when tall.
  - Edit the JSON, Save → applied banner + Undo; open Change Log → entry present; Undo reverts.
  - Click a plugin row → read-only note, no Save.

- [ ] **Step 7: Commit**

```bash
git add src/ClaudeExplorer.App/Pages/Hooks.razor src/ClaudeExplorer.App/wwwroot/css/blueprint.css
git commit -m "feat(app): Hooks row redesign (chips) + inline JSON editor with highlighted script file"
```

---

### Task 8: Full verification

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test ClaudeExplorer.slnx`
Expected: all green, including the new `MatcherChipsTests`, `HookRowsTests`, `HookScriptResolverTests`, `HookBlockEditorTests`, `HookEditViewModelTests`.

- [ ] **Step 2: Build the app once more**

Run: `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Update the roadmap/HANDOFF** — mark this phase done in `docs/superpowers/plans/2026-06-07-00-roadmap.md` and `docs/superpowers/HANDOFF.md` (test count + tip commit), matching the existing phrasing.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers
git commit -m "docs: mark hooks inline-editor phase done"
```

---

## Self-review checklist (done while writing)
- **Spec coverage:** row chips (T1/T2/T7), inline accordion under row (T7), formatted editable JSON (T4 extract pretty + T7 textarea), referenced file highlighted (T3 + T6 + T7), both views scroll (T6 `Capped` + T7 `.json-edit` max-height), splice-via-safe-mutation (T4/T5), plugin/enterprise read-only (T2 `IsEditable` + T5 + T7), highlight.js bundled (T6), edit-defining-file only / no project override (T5 uses `EditMode.EditWinner` only). ✓
- **Type consistency:** `MatcherChip`/`HookMatcher.Chips`, `HookRow.SourceGroupIndex`/`IsEditable`, `ScriptRef(Path,Language,Exists)`, `HookScriptResolver.Resolve(fs,command,srcDir,projDir,userDir)`, `HookBlockEditor.ExtractBlock/SpliceBlock(...,sourceGroupIndex,...)`, `HookEditViewModel(svc,fs,row,nowIso,projectDir)` — names match across tasks. ✓
- **Out of scope (unchanged):** live-coloring while typing (textarea only), project/local override for hooks, add/delete hooks, markdown editor.
