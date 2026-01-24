using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;

namespace Avalonia.Controls.DataGridTests;

internal static class LeakTestSession
{
    internal static void RunInSession(Action action)
        => RunInSession(typeof(LeakTestsApp), action);

    internal static void RunInSession(Type appType, Action action)
    {
        using var session = HeadlessUnitTestSession.StartNew(appType);
        try
        {
            session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static T RunInSession<T>(Func<T> action)
        => RunInSession(typeof(LeakTestsApp), action);

    internal static T RunInSession<T>(Type appType, Func<T> action)
    {
        using var session = HeadlessUnitTestSession.StartNew(appType);
        try
        {
            return session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static Task RunInSessionAsync(Func<Task> action)
        => RunInSessionAsync(typeof(LeakTestsApp), action);

    internal static async Task RunInSessionAsync(Type appType, Func<Task> action)
    {
        using var session = HeadlessUnitTestSession.StartNew(appType);
        try
        {
            await session.Dispatch(action, CancellationToken.None);
        }
        finally
        {
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static Task<T> RunInSessionAsync<T>(Func<Task<T>> action)
        => RunInSessionAsync(typeof(LeakTestsApp), action);

    internal static async Task<T> RunInSessionAsync<T>(Type appType, Func<Task<T>> action)
    {
        using var session = HeadlessUnitTestSession.StartNew(appType);
        try
        {
            return await session.Dispatch(action, CancellationToken.None);
        }
        finally
        {
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }
}
