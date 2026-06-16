using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Diagnostics;

public class DevToolsOpenWindowTests
{
    [AvaloniaFact]
    public void Open_Focuses_Existing_Window_When_New_Group_Shares_Inspected_TopLevel()
    {
        var owner = new Window();
        var secondary = new Window();
        var options = new DevToolsOptions();
        var initialOpenWindowCount = DevTools.OpenWindowCount;
        IDisposable? firstOpen = null;

        try
        {
            firstOpen = DevTools.Open(new SingleViewTopLevelGroup(owner), options);

            Assert.Equal(initialOpenWindowCount + 1, DevTools.OpenWindowCount);

            using var secondOpen = DevTools.Open(new TestTopLevelGroup(owner, secondary), options);

            Assert.Equal(initialOpenWindowCount + 1, DevTools.OpenWindowCount);
        }
        finally
        {
            firstOpen?.Dispose();
        }
    }

    [AvaloniaFact]
    public void SharesTopLevel_Returns_True_When_Single_Window_Group_Opened_Before_Application_Group()
    {
        var owner = new Window();
        var secondary = new Window();
        var singleWindowGroup = new TestTopLevelGroup(owner);
        var applicationGroup = new TestTopLevelGroup(owner, secondary);

        Assert.True(DevTools.SharesTopLevel(applicationGroup, singleWindowGroup));
    }

    [AvaloniaFact]
    public void SharesTopLevel_Returns_True_When_Application_Group_Opened_Before_Single_Window_Group()
    {
        var owner = new Window();
        var secondary = new Window();
        var applicationGroup = new TestTopLevelGroup(owner, secondary);
        var singleWindowGroup = new TestTopLevelGroup(owner);

        Assert.True(DevTools.SharesTopLevel(singleWindowGroup, applicationGroup));
    }

    [AvaloniaFact]
    public void SharesTopLevel_Returns_False_When_Groups_Do_Not_Overlap()
    {
        var firstGroup = new TestTopLevelGroup(new Window());
        var secondGroup = new TestTopLevelGroup(new Window());

        Assert.False(DevTools.SharesTopLevel(firstGroup, secondGroup));
    }

    private sealed class TestTopLevelGroup : IDevToolsTopLevelGroup
    {
        public TestTopLevelGroup(params TopLevel[] items)
        {
            Items = items;
        }

        public IReadOnlyList<TopLevel> Items { get; }
    }
}
