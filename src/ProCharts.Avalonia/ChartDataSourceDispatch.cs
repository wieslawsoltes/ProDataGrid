// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia.Threading;
using ProCharts;

namespace ProCharts.Avalonia
{
    /// <summary>Creates an explicit producer-to-consumer notification boundary for Avalonia chart models.</summary>
    public static class ChartDataSourceDispatch
    {
        /// <summary>Coalesces source events onto the UI dispatcher, using Background priority unless specified.</summary>
        /// <remarks>
        /// Call on the UI thread, assign the returned adapter to ChartModel.DataSource, and retain/dispose it with
        /// the model. This does not own the source or make a non-thread-safe source safe for worker mutation.
        /// Post scheduling is not a timer, fixed-rate throttle, vsync callback or guarantee of one render per frame.
        /// </remarks>
        public static CoalescingChartDataSource Create(IChartDataSource source, DispatcherPriority? priority = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            Dispatcher dispatcher = Dispatcher.UIThread;
            dispatcher.VerifyAccess();
            return new CoalescingChartDataSource(source,
                new AvaloniaSynchronizationContext(dispatcher, priority ?? DispatcherPriority.Background));
        }
    }
}
