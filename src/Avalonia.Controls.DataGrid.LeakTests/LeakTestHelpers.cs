using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

internal static class LeakTestHelpers
{
    internal static void AssertCollected(params WeakReference[] references)
    {
        for (var attempt = 0; attempt < 20 && references.Any(reference => reference.IsAlive); attempt++)
        {
            CollectGarbage();
            Thread.Sleep(10);
        }

        foreach (var reference in references)
        {
            Assert.False(reference.IsAlive);
        }
    }

    internal static void ResetHeadlessCompositor()
    {
        var type = Type.GetType("Avalonia.Headless.AvaloniaHeadlessPlatform, Avalonia.Headless");
        var property = type?.GetProperty("Compositor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(null, null);
    }

    internal static void CleanupWindow(Window window)
    {
        window.FocusManager?.ClearFocus();
        window.Content = null;
        window.DataContext = null;
        if (window is IInputRoot inputRoot)
        {
            inputRoot.PointerOverElement = null;
        }
        for (var i = 0; i < 3; i++)
        {
            ExecuteLayoutPass(window);
            RunJobsAndRender();
        }
        window.Close();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        RunJobsAndRender();
    }

    internal static void ShowWindow(Window window)
    {
        if (window.SizeToContent != SizeToContent.Manual)
        {
            window.SizeToContent = SizeToContent.Manual;
        }

        if (window.SizeToContent == SizeToContent.Manual)
        {
            if (double.IsNaN(window.Width) || window.Width <= 0)
            {
                window.Width = 800;
            }

            if (double.IsNaN(window.Height) || window.Height <= 0)
            {
                window.Height = 600;
            }
        }

        window.Show();
        for (var i = 0; i < 3; i++)
        {
            ExecuteLayoutPass(window);
            RunJobsAndRender();
        }

        window.UpdateLayout();
        RunJobsAndRender();
    }

    internal static void CollectGarbage()
    {
        // KeyboardDevice keeps a reference to the focused element.
        var keyboard = typeof(KeyboardDevice)
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null);
        var setFocusedElement = keyboard?.GetType().GetMethod(
            "SetFocusedElement",
            new[] { typeof(IInputElement), typeof(NavigationMethod), typeof(KeyModifiers) });
        setFocusedElement?.Invoke(keyboard, new object?[] { null, NavigationMethod.Unspecified, KeyModifiers.None });
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        RunJobsAndRender();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        RunJobsAndRender();
        GC.Collect();
    }

    internal static void ResetLoadedQueueForUnitTests()
    {
        var resetMethod = typeof(Control).GetMethod(
            "ResetLoadedQueueForUnitTests",
            BindingFlags.Static | BindingFlags.NonPublic);
        resetMethod?.Invoke(null, null);
    }


    internal static void StopDispatcherTimers()
    {
        try
        {
            var snapshotMethod = typeof(Dispatcher).GetMethod(
                "SnapshotTimersForUnitTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (snapshotMethod?.Invoke(null, null) is IEnumerable timers)
            {
                foreach (var timer in timers)
                {
                    if (timer is DispatcherTimer dispatcherTimer)
                    {
                        dispatcherTimer.Stop();
                    }
                }
            }
        }
        catch
        {
            // Dispatcher may already be reset/disposed in some runs.
        }
    }

    internal static object? GetPrivateField(object instance, string fieldName)
    {
        var type = instance.GetType();
        FieldInfo? field = null;
        while (type != null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                break;
            }
            type = type.BaseType;
        }
        Assert.NotNull(field);
        return field!.GetValue(instance);
    }

    internal static void ExecuteLayoutPass(Window window)
    {
        PropertyInfo? layoutProperty = null;
        for (var type = window.GetType(); type != null && layoutProperty == null; type = type.BaseType)
        {
            layoutProperty = type.GetProperty(
                "LayoutManager",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        var layoutManager = layoutProperty?.GetValue(window);
        var executeMethod = layoutManager?.GetType().GetMethod(
            "ExecuteLayoutPass",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        executeMethod?.Invoke(layoutManager, null);
    }

    internal static void RunJobsAndRender()
    {
        var dispatcher = Dispatcher.UIThread;
        for (var i = 0; i < 10; i++)
        {
            dispatcher.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (!dispatcher.HasJobsWithPriority(DispatcherPriority.SystemIdle))
            {
                return;
            }
        }
        dispatcher.RunJobs();
    }

    internal static void PumpLayout(Control control)
    {
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
    }

    internal static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    internal static void InvokePrivateMethod(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, null);
    }

    internal static void InvokePrivateMethod(object instance, string methodName, Type[] parameterTypes, object[] arguments)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.NotNull(method);
        method!.Invoke(instance, arguments);
    }
}
