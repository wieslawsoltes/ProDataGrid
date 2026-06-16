using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Diagnostics;

public class DevToolsOpenWindowTests
{
    [AvaloniaFact]
    public void Open_Focuses_Existing_Window_When_New_Group_Shares_Inspected_TopLevel()
    {
        CloseOpenDevToolsWindows();

        var owner = new Window();
        var secondary = new Window();
        var options = new DevToolsOptions();
        IDisposable? firstOpen = null;

        try
        {
            firstOpen = DevTools.Open(new SingleViewTopLevelGroup(owner), options);

            Assert.Equal(1, GetOpenDevToolsWindowCount());

            using var secondOpen = DevTools.Open(new TestTopLevelGroup(owner, secondary), options);

            Assert.Equal(1, GetOpenDevToolsWindowCount());
        }
        finally
        {
            firstOpen?.Dispose();
            CloseOpenDevToolsWindows();
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

    private static int GetOpenDevToolsWindowCount()
    {
        return GetOpenDevToolsWindows().Count;
    }

    private static void CloseOpenDevToolsWindows()
    {
        var windows = GetOpenDevToolsWindows();

        for (int i = 0; i < windows.Count; i++)
        {
            windows[i].Close();
        }
    }

    private static IReadOnlyList<Window> GetOpenDevToolsWindows()
    {
        var field = typeof(DevTools).GetField("s_open", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to locate DevTools open-window registry.");
        var dictionary = (System.Collections.IDictionary)field.GetValue(null)!;
        var windows = new List<Window>(dictionary.Count);

        foreach (Window window in dictionary.Values)
        {
            windows.Add(window);
        }

        return windows;
    }
}
