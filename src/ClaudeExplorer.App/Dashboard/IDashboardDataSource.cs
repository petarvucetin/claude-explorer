namespace ClaudeExplorer.App.Dashboard;

/// <summary>Gathers raw <see cref="DashboardInputs"/> for the current workspace. The engine impl
/// touches the file system / process runner, so it is not unit-tested; ViewModels are tested
/// against a fake.</summary>
public interface IDashboardDataSource
{
    DashboardInputs GetInputs();
}
