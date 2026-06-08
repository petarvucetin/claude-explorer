using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Screens.Artifacts;
using ClaudeExplorer.App.Screens.ChangeLog;
using ClaudeExplorer.App.Screens.Dependencies;
using ClaudeExplorer.App.Screens.EffectiveConfig;
using ClaudeExplorer.App.Screens.Hooks;
using ClaudeExplorer.App.Screens.Marketplace;
using ClaudeExplorer.App.Screens.Mcp;
using ClaudeExplorer.App.Screens.Plugins;
using ClaudeExplorer.App.Screens.Recommendations;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.ViewModels;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mcp;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Plugins;
using ClaudeExplorer.Core.Recommendations;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Photino.Blazor;

namespace ClaudeExplorer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging();
        builder.Services.AddMudServices();

        // Core seams (real machine impls). One PhysicalFileSystem instance is reused for both the
        // DI seam and the startup workspace resolution below.
        var fileSystem = new PhysicalFileSystem();
        builder.Services.AddSingleton<IFileSystem>(fileSystem);
        builder.Services.AddSingleton<IFileWriter, PhysicalFileWriter>();
        builder.Services.AddSingleton<IPathResolver, PhysicalPathResolver>();
        builder.Services.AddSingleton<IProcessRunner>(_ => new PhysicalProcessRunner());

        // Environments.
        var winHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        builder.Services.AddSingleton<IWslLocator>(sp => new WslLocator(sp.GetRequiredService<IProcessRunner>()));
        builder.Services.AddSingleton(sp => new EnvironmentDiscovery(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IWslLocator>(), winHome));
        builder.Services.AddSingleton(sp => new EnvironmentStore(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            $"{winHome.Replace('\\', '/')}/.claude/.claude-explorer/environments.json"));
        builder.Services.AddSingleton(sp =>
        {
            var svc = new EnvironmentService(sp.GetRequiredService<EnvironmentDiscovery>(), sp.GetRequiredService<EnvironmentStore>());
            svc.Load();
            return svc;
        });

        // IWorkspaceContext is now driven by the active environment (replaces the old fixed WorkspaceContext).
        // WorkspaceResolver is retained for future "open project" wiring.
        builder.Services.AddSingleton<IWorkspaceContext>(sp =>
            new ActiveEnvironmentWorkspaceContext(sp.GetRequiredService<EnvironmentService>()));

        // Core façades.
        builder.Services.AddSingleton(sp => new EffectiveConfigService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new ArtifactCatalogService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new DependencyHealthService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IPathResolver>(), sp.GetRequiredService<IProcessRunner>()));
        builder.Services.AddSingleton(sp => new McpServerReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new McpInventoryReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new PluginInventoryReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton<IBackupStore>(sp => new FileBackupStore(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/')}/.claude/.claude-explorer/backups"));
        builder.Services.AddSingleton(sp => new SafeMutationService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            sp.GetRequiredService<IBackupStore>(), sp.GetRequiredService<IProcessRunner>()));

        // App-wide services.
        builder.Services.AddSingleton<RefreshService>();

        // Clock seam: injectable timestamp factory (tests pass a fixed value instead).
        builder.Services.AddSingleton<Func<string>>(_ => () => DateTime.UtcNow.ToString("o"));

        // Dashboard data + view models.
        builder.Services.AddSingleton<IDashboardDataSource, EngineDashboardDataSource>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ShellViewModel>();

        // Catalog + Recommendations façades.
        builder.Services.AddSingleton<ICatalogFetcher, HttpCatalogFetcher>();
        builder.Services.AddSingleton(sp => new CatalogService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<ICatalogFetcher>()));
        builder.Services.AddSingleton(sp => new RecommendationService(sp.GetRequiredService<IFileSystem>()));

        // Batch-A screen ViewModels (transient; each page owns its own instance).
        // Note: SafeEditViewModel is NOT registered in DI — EffectiveConfig.razor creates it
        // via `new` so it can pass the winning SettingOrigin and real projectDir at runtime.
        builder.Services.AddTransient<EffectiveConfigViewModel>();
        builder.Services.AddTransient<ArtifactBrowserViewModel>();
        builder.Services.AddTransient<DependencyViewModel>();
        builder.Services.AddTransient<ChangeLogViewModel>();
        builder.Services.AddTransient<McpViewModel>();
        builder.Services.AddTransient<PluginsViewModel>();
        builder.Services.AddTransient<HooksViewModel>();

        // Batch-B screen ViewModels (transient).
        builder.Services.AddTransient(sp => new MarketplaceViewModel(
            sp.GetRequiredService<CatalogService>(),
            sp.GetRequiredService<SafeMutationService>(),
            sp.GetRequiredService<IWorkspaceContext>(),
            sp.GetRequiredService<Func<string>>(),
            sp.GetRequiredService<DependencyHealthService>()));
        builder.Services.AddTransient(sp => new RecommendationsViewModel(
            sp.GetRequiredService<CatalogService>(),
            sp.GetRequiredService<RecommendationService>(),
            sp.GetRequiredService<IWorkspaceContext>()));

        // Compare.
        builder.Services.AddSingleton(sp => new InstalledPluginsReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton<IEnvironmentCompareDataSource, EngineEnvironmentCompareDataSource>();
        builder.Services.AddTransient<CompareViewModel>();

        builder.RootComponents.Add<App>("app");

        var app = builder.Build();

        app.MainWindow
            .SetTitle("Claude Explorer")
            .SetUseOsDefaultSize(false)
            .SetSize(1320, 860)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
            app.MainWindow.ShowMessage("Fatal Exception", error.ExceptionObject?.ToString() ?? "Unknown error");

        app.Run();
    }
}
