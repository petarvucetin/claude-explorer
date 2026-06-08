using ClaudeExplorer.App.Screens.Marketplace;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Screens;

public class MarketplaceViewModelTests
{
    private const string UserDir = "/home/user";
    private const string ProjectDir = "/home/user/project";
    private const string BackupRoot = "/backups";

    // ── helpers ────────────────────────────────────────────────────────────────

    private static SafeMutationService BuildMutation(FakeProcessRunner? runner = null)
    {
        var fs = new InMemoryFileSystem();
        var backupFs = new InMemoryFileSystem();
        var backupStore = new FileBackupStore(backupFs, backupFs, BackupRoot);
        return new SafeMutationService(fs, fs, backupStore, runner ?? new FakeProcessRunner());
    }

    private static FakeWorkspaceContext BuildWorkspace()
        => new(UserDir, ProjectDir);

    // A minimal "installed" marketplace.json that the InstalledMarketplaceReader can find.
    // The reader looks in userDir/.claude/plugins/marketplaces/<name>/.claude-plugin/marketplace.json
    private static InMemoryFileSystem WithInstalledMarketplace()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/home/user/.claude/plugins/marketplaces/test-market/.claude-plugin/marketplace.json",
            """{"name":"Test Market","plugins":[{"name":"cool-plugin","description":"A cool plugin"}]}""");
        return fs;
    }

    private static MarketplaceViewModel BuildVm(
        ICatalogFetcher? fetcher = null,
        FakeProcessRunner? runner = null,
        InMemoryFileSystem? installedFs = null)
    {
        var fs = installedFs ?? new InMemoryFileSystem();
        var catalog = new CatalogService(fs, fetcher ?? new FakeCatalogFetcher());
        var mutation = BuildMutation(runner);
        return new MarketplaceViewModel(catalog, mutation, BuildWorkspace(), () => "2026-06-07T00:00:00Z");
    }

    // ── mapper unit tests ──────────────────────────────────────────────────────

    [Fact]
    public void Mapper_maps_name_type_trust_author()
    {
        var source = new CatalogSource(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "Official", "/path");
        var items = new[]
        {
            new CatalogItem("my-plugin", CatalogItemType.Plugin, "Summary", "Alice",
                null, null, Array.Empty<string>(), source, TrustLevel.Verified),
        };

        var rows = MarketplaceMapper.Map(items);

        Assert.Single(rows);
        Assert.Equal("my-plugin", rows[0].Name);
        Assert.Equal(CatalogItemType.Plugin, rows[0].Type);
        Assert.Equal(TrustLevel.Verified, rows[0].Trust);
        Assert.Equal("Alice", rows[0].Author);
        Assert.Equal("Official", rows[0].SourceName);
    }

    [Fact]
    public void Mapper_InstallArgs_and_UninstallArgs_correct()
    {
        var install = MarketplaceMapper.InstallArgs("my-plugin");
        var uninstall = MarketplaceMapper.UninstallArgs("my-plugin");

        Assert.Equal(new[] { "plugin", "install", "my-plugin" }, install);
        Assert.Equal(new[] { "plugin", "uninstall", "my-plugin" }, uninstall);
    }

    [Fact]
    public void Mapper_TypeLabel_returns_string_for_each_type()
    {
        Assert.Equal("Plugin", MarketplaceMapper.TypeLabel(CatalogItemType.Plugin));
        Assert.Equal("Skill",  MarketplaceMapper.TypeLabel(CatalogItemType.Skill));
        Assert.Equal("Agent",  MarketplaceMapper.TypeLabel(CatalogItemType.Agent));
    }

    // ── ViewModel behaviour ────────────────────────────────────────────────────

    [Fact]
    public void LoadInstalled_populates_Items()
    {
        var fs = WithInstalledMarketplace();
        var vm = BuildVm(installedFs: fs);

        vm.LoadInstalled();

        Assert.NotEmpty(vm.Items);
        Assert.Equal("cool-plugin", vm.Items[0].Name);
        Assert.Null(vm.Error);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void LoadInstalled_empty_filesystem_gives_empty_items()
    {
        var vm = BuildVm();

        vm.LoadInstalled();

        Assert.Empty(vm.Items);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void AddSource_via_fake_fetcher_populates_AddedItems_with_community_trust()
    {
        var manifest = """{"name":"Community Src","plugins":[{"name":"ext-tool","description":"External tool"}]}""";
        var url = "https://example.com/source";
        var manifestUrl = "https://example.com/source/.claude-plugin/marketplace.json";
        var fetcher = new FakeCatalogFetcher().Add(manifestUrl, manifest);
        var vm = BuildVm(fetcher: fetcher);

        vm.AddSource(url);

        Assert.Single(vm.AddedItems);
        Assert.Equal("ext-tool", vm.AddedItems[0].Name);
        Assert.Equal(TrustLevel.Community, vm.AddedItems[0].Trust);
        Assert.True(vm.ShowTrustWarning);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void AddSource_unreachable_url_gives_empty_AddedItems_no_crash()
    {
        var vm = BuildVm(fetcher: new FakeCatalogFetcher()); // no entries → null response

        vm.AddSource("https://missing.example.com/");

        Assert.Empty(vm.AddedItems);
        Assert.False(vm.ShowTrustWarning);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void Install_successful_records_ChangeLogEntry()
    {
        var runner = new FakeProcessRunner().AddVersion("claude", "1.0", exitCode: 0);
        var vm = BuildVm(runner: runner);
        vm.LoadInstalled();

        var source = new CatalogSource(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "Src", "loc");
        var row = new MarketplaceItemRow("my-tool", CatalogItemType.Plugin, null, null, TrustLevel.Verified,
            CatalogSourceKind.ClaudeMarketplace, "Src");

        vm.Install(row);

        Assert.NotNull(vm.LastInstall);
        Assert.Equal("my-tool", vm.LastInstall!.FilePath);
        Assert.Null(vm.Error);
        // Verify the CLI was called with the right args
        Assert.Contains(runner.Invocations, inv =>
            inv.Executable == "claude" &&
            inv.Arguments.SequenceEqual(new[] { "plugin", "install", "my-tool" }));
    }

    [Fact]
    public void Install_nonzero_exit_sets_Error_not_throws()
    {
        var runner = new FakeProcessRunner().AddResult("claude", new ClaudeExplorer.Core.Dependencies.ProcessResult(-1, "", "not found"));
        var vm = BuildVm(runner: runner);
        var row = new MarketplaceItemRow("bad-tool", CatalogItemType.Plugin, null, null, TrustLevel.Community,
            CatalogSourceKind.GitHub, "src");

        vm.Install(row);

        Assert.Null(vm.LastInstall);
        Assert.NotNull(vm.Error);
    }

    [Fact]
    public void UndoLastInstall_marks_entry_undone()
    {
        // First install (succeeds), then undo (also calls claude for uninstall)
        var runner = new FakeProcessRunner().AddVersion("claude", "1.0", exitCode: 0);
        var vm = BuildVm(runner: runner);
        var row = new MarketplaceItemRow("my-tool", CatalogItemType.Plugin, null, null, TrustLevel.Verified,
            CatalogSourceKind.ClaudeMarketplace, "Src");

        vm.Install(row);
        Assert.NotNull(vm.LastInstall);

        vm.UndoLastInstall();

        Assert.True(vm.LastInstall!.IsUndone);
        Assert.Null(vm.Error);
    }
}

// Minimal fake workspace context for App tests
internal sealed class FakeWorkspaceContext : ClaudeExplorer.App.Services.IWorkspaceContext
{
    public string UserDir { get; }
    public string ProjectDir { get; }
    public string ProjectLabel { get; }

    public FakeWorkspaceContext(string userDir, string projectDir)
    {
        UserDir = userDir;
        ProjectDir = projectDir;
        ProjectLabel = projectDir.Split('/').Last();
    }
}
