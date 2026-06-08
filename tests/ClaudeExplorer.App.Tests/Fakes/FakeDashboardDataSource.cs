using ClaudeExplorer.App.Dashboard;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeDashboardDataSource : IDashboardDataSource
{
    private readonly DashboardInputs _inputs;
    public int Calls { get; private set; }
    public FakeDashboardDataSource(DashboardInputs inputs) => _inputs = inputs;
    public DashboardInputs GetInputs() { Calls++; return _inputs; }
}
