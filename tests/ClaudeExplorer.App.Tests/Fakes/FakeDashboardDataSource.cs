using ClaudeExplorer.App.Dashboard;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeDashboardDataSource : IDashboardDataSource
{
    private readonly DashboardInputs? _inputs;
    private readonly bool _shouldThrow;
    private readonly string _throwMessage;

    public int Calls { get; private set; }

    public FakeDashboardDataSource(DashboardInputs inputs)
    {
        _inputs = inputs;
        _shouldThrow = false;
        _throwMessage = string.Empty;
    }

    /// <summary>Creates a source that throws when <see cref="GetInputs"/> is called.</summary>
    public FakeDashboardDataSource(string throwMessage)
    {
        _inputs = null;
        _shouldThrow = true;
        _throwMessage = throwMessage;
    }

    public DashboardInputs GetInputs()
    {
        Calls++;
        if (_shouldThrow)
            throw new InvalidOperationException(_throwMessage);
        return _inputs!;
    }
}
