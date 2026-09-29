// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProCharts;

namespace ProCharts.Avalonia
{
    /// <summary>Opt-in pointer and keyboard navigation for a ProChartView displaying a typed hierarchy.</summary>
    /// <remarks>
    /// Double-click a branch to enter it, Backspace or Alt+Left to return, and Home to reset.
    /// The default is disabled. Matrix projections are not navigable. Stale displayed roots are rejected.
    /// Programmatic methods do not require enabling gestures; custom sources implement IChartHierarchyNavigator.
    /// </remarks>
    public sealed class ChartHierarchyNavigation : AvaloniaObject
    {
        /// <summary>Enables hierarchy navigation without changing Cartesian pointer workflows.</summary>
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<ChartHierarchyNavigation, ProChartView, bool>("IsEnabled");

        static ChartHierarchyNavigation()
        {
            IsEnabledProperty.Changed.AddClassHandler<ProChartView>(OnEnabledChanged);
        }

        private ChartHierarchyNavigation() { }

        /// <summary>Gets whether hierarchy gestures are enabled.</summary>
        public static bool GetIsEnabled(ProChartView view) => view.GetValue(IsEnabledProperty);
        /// <summary>Enables or disables hierarchy gestures, attaching or removing view-local handlers.</summary>
        public static void SetIsEnabled(ProChartView view, bool value) => view.SetValue(IsEnabledProperty, value);

        /// <summary>Enters the branch painted at a point in local logical coordinates.</summary>
        public static bool TryDrillDown(ProChartView view, Point point)
        {
            ArgumentNullException.ThrowIfNull(view);
            if (!TryGetNavigator(view, out var navigator, out var hierarchy)) return false;
            var hit = view.HitTest(point);
            if (!hit.HasValue || hit.Value.PointIndex <= 0 || hit.Value.PointIndex >= hierarchy.Nodes.Count) return false;
            return navigator.TryDrillDown(hierarchy.Nodes[hit.Value.PointIndex].Id);
        }

        /// <summary>Returns to the previous root when the displayed snapshot still matches the source.</summary>
        public static bool TryDrillUp(ProChartView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            return TryGetNavigator(view, out var navigator, out _) && navigator.TryDrillUp();
        }

        /// <summary>Returns to the original root when the displayed snapshot still matches the source.</summary>
        public static bool Reset(ProChartView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            return TryGetNavigator(view, out var navigator, out _) && navigator.ResetNavigation();
        }

        private static bool TryGetNavigator(ProChartView view, out IChartHierarchyNavigator navigator,
            out ChartHierarchySnapshot hierarchy)
        {
            navigator = null!;
            hierarchy = null!;
            var model = view.ChartModel;
            if (model?.DataSource is not IChartHierarchyNavigator source || model.Snapshot.Hierarchy is not { } displayed ||
                !ReferenceEquals(source.CurrentRoot, displayed.Root)) return false;
            navigator = source;
            hierarchy = displayed;
            return true;
        }

        private static void OnEnabledChanged(ProChartView view, AvaloniaPropertyChangedEventArgs change)
        {
            view.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            view.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            if (change.NewValue is not true) return;
            view.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            view.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not ProChartView view || e.ClickCount != 2 || e.KeyModifiers != KeyModifiers.None ||
                !e.GetCurrentPoint(view).Properties.IsLeftButtonPressed || !TryGetNavigator(view, out _, out _)) return;
            view.Focus();
            TryDrillDown(view, e.GetPosition(view));
            // A leaf or gap is not a request to reset the Cartesian window behind this hierarchy.
            e.Handled = true;
        }

        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not ProChartView view || !view.EnableKeyboardNavigation || !TryGetNavigator(view, out _, out _)) return;
            if ((e.Key == Key.Back && e.KeyModifiers == KeyModifiers.None) ||
                (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt))
            {
                TryDrillUp(view);
                e.Handled = true;
            }
            else if (e.Key == Key.Home && e.KeyModifiers == KeyModifiers.None)
            {
                Reset(view);
                e.Handled = true;
            }
        }
    }
}
