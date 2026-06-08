using System.ComponentModel;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.Tests.Mvvm;

public class ObservableObjectTests
{
    private sealed class Sample : ObservableObject
    {
        private int _value;
        public int Value { get => _value; set => SetProperty(ref _value, value); }
    }

    [Fact]
    public void SetProperty_raises_PropertyChanged_with_the_property_name()
    {
        var sample = new Sample();
        var raised = new List<string?>();
        sample.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sample.Value = 5;

        Assert.Equal(new[] { nameof(Sample.Value) }, raised);
        Assert.Equal(5, sample.Value);
    }

    [Fact]
    public void SetProperty_does_not_raise_when_value_is_unchanged()
    {
        var sample = new Sample { Value = 5 };
        var raised = 0;
        sample.PropertyChanged += (_, _) => raised++;

        sample.Value = 5;

        Assert.Equal(0, raised);
    }
}
