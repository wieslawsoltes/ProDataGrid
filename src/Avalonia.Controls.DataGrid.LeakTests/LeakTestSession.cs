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
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(appType);
            session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            DrainSession(session);
        }
        finally
        {
            session?.Dispose();
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static T RunInSession<T>(Func<T> action)
        => RunInSession(typeof(LeakTestsApp), action);

    internal static T RunInSession<T>(Type appType, Func<T> action)
    {
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(appType);
            var result = session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            DrainSession(session);
            return result;
        }
        finally
        {
            session?.Dispose();
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static Task RunInSessionAsync(Func<Task> action)
        => RunInSessionAsync(typeof(LeakTestsApp), action);

    internal static async Task RunInSessionAsync(Type appType, Func<Task> action)
    {
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(appType);
            await session.Dispatch(action, CancellationToken.None);
            DrainSession(session);
        }
        finally
        {
            session?.Dispose();
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    internal static Task<T> RunInSessionAsync<T>(Func<Task<T>> action)
        => RunInSessionAsync(typeof(LeakTestsApp), action);

    internal static async Task<T> RunInSessionAsync<T>(Type appType, Func<Task<T>> action)
    {
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(appType);
            var result = await session.Dispatch(action, CancellationToken.None);
            DrainSession(session);
            return result;
        }
        finally
        {
            session?.Dispose();
            LeakTestHelpers.ResetHeadlessCompositor();
            LeakTestHelpers.ResetLoadedQueueForUnitTests();
            LeakTestHelpers.StopDispatcherTimers();
        }
    }

    private static void DrainSession(HeadlessUnitTestSession session)
    {
        session.Dispatch(static () =>
        {
            LeakTestHelpers.StopDispatcherTimers();
            LeakTestHelpers.DrainDispatcher();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
}
