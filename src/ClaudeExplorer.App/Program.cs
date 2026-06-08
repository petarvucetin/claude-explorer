using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.ViewModels;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;
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

        // Core seams (real machine impls).
        builder.Services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        builder.Services.AddSingleton<IFileWriter, PhysicalFileWriter>();
        builder.Services.AddSingleton<IPathResolver, PhysicalPathResolver>();
        builder.Services.AddSingleton<IProcessRunner>(_ => new PhysicalProcessRunner());

        // Workspace: user home (holds ~/.claude) + current dir as the active project.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var project = Directory.GetCurrentDirectory();
        builder.Services.AddSingleton<IWorkspaceContext>(new WorkspaceContext(home, project));

        // Core façades.
        builder.Services.AddSingleton(sp => new EffectiveConfigService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new ArtifactCatalogService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new DependencyHealthService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IPathResolver>(), sp.GetRequiredService<IProcessRunner>()));
        builder.Services.AddSingleton(sp => new McpServerReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton<IBackupStore>(sp => new FileBackupStore(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/')}/.claude/.claude-explorer/backups"));
        builder.Services.AddSingleton(sp => new SafeMutationService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            sp.GetRequiredService<IBackupStore>(), sp.GetRequiredService<IProcessRunner>()));

        // Dashboard data + view models.
        builder.Services.AddSingleton<IDashboardDataSource, EngineDashboardDataSource>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ShellViewModel>();

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
