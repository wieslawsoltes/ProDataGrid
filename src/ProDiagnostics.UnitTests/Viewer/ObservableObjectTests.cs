using System.Collections.Generic;
using System.ComponentModel;
using ProDiagnostics.Viewer.ViewModels;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer;

public class ObservableObjectTests
{
    [Fact]
    public void SetProperty_Raises_PropertyChanged()
    {
        var changes = new List<string?>();
        var test = new TestObject();
        test.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        test.Name = "Updated";

        Assert.Single(changes);
        Assert.Equal(nameof(TestObject.Name), changes[0]);
    }

    [Fact]
    public void RelayCommand_Execute_And_CanExecute()
    {
        var executed = false;
        var canExecute = false;
        var command = new RelayCommand(() => executed = true, () => canExecute);

        Assert.False(command.CanExecute(null));

        canExecute = true;
        Assert.True(command.CanExecute(null));

        command.Execute(null);
        Assert.True(executed);
    }

    private sealed class TestObject : ObservableObject
    {
        private string _name = "Initial";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }
}
