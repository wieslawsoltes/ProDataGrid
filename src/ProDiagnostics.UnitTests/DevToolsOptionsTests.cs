using Avalonia.Diagnostics;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class DevToolsOptionsTests
{
    [Fact]
    public void Defaults_Use_F12_For_InProcess_And_F11_For_Remote_Launch()
    {
        var options = new DevToolsOptions();

        Assert.Equal(Key.F12, options.Gesture.Key);
        Assert.Equal(KeyModifiers.None, options.Gesture.KeyModifiers);
        Assert.Equal(Key.F11, options.RemoteGesture.Key);
        Assert.Equal(KeyModifiers.None, options.RemoteGesture.KeyModifiers);
        Assert.True(options.EnableRemoteGesture);
        Assert.False(options.UseRemoteRuntime);
    }
}
