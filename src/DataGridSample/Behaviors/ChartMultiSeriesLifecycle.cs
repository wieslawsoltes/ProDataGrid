// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DataGridSample.ViewModels;

namespace DataGridSample.Behaviors
{
    /// <summary>Supplies visual lifetime and UI-thread timer scheduling to the otherwise UI-independent feed view model.</summary>
    public sealed class ChartMultiSeriesLifecycle : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<ChartMultiSeriesLifecycle, Control, bool>("IsEnabled");
        private static readonly AttachedProperty<Session?> SessionProperty =
            AvaloniaProperty.RegisterAttached<ChartMultiSeriesLifecycle, Control, Session?>("Session");

        static ChartMultiSeriesLifecycle()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>((control, _) =>
            {
                control.GetValue(SessionProperty)?.Dispose();
                control.SetValue(SessionProperty, GetIsEnabled(control) ? new Session(control) : null);
            });
        }

        public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

        private sealed class Session : IDisposable
        {
            private readonly Control _control;
            private ChartMultiSeriesStreamingViewModel? _model;
            private DispatcherTimer? _timer;

            public Session(Control control)
            {
                _control = control;
                control.AttachedToVisualTree += Attached;
                control.DetachedFromVisualTree += Detached;
                control.DataContextChanged += ContextChanged;
                if (TopLevel.GetTopLevel(control) != null)
                {
                    Start();
                }
            }

            public void Dispose()
            {
                _control.AttachedToVisualTree -= Attached;
                _control.DetachedFromVisualTree -= Detached;
                _control.DataContextChanged -= ContextChanged;
                Stop();
            }

            private void Attached(object? sender, VisualTreeAttachmentEventArgs e) => Start();
            private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();

            private void ContextChanged(object? sender, EventArgs e)
            {
                Stop();
                if (TopLevel.GetTopLevel(_control) != null)
                {
                    Start();
                }
            }

            private void Start()
            {
                if (_model != null || _control.DataContext is not ChartMultiSeriesStreamingViewModel model)
                {
                    return;
                }

                _model = model;
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _timer.Tick += Tick;
                model.PropertyChanged += StateChanged;
                try
                {
                    model.Activate(new AvaloniaSynchronizationContext(Dispatcher.UIThread, DispatcherPriority.Background));
                }
                catch
                {
                    Stop();
                    throw;
                }
            }

            private void Stop()
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= Tick;
                    _timer = null;
                }

                if (_model != null)
                {
                    ChartMultiSeriesStreamingViewModel model = _model;
                    _model = null;
                    model.PropertyChanged -= StateChanged;
                    model.Deactivate();
                }
            }

            private void Tick(object? sender, EventArgs e)
            {
                if (_model?.IsRunning == true)
                {
                    _model.AppendBatch();
                }
            }

            private void StateChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(ChartMultiSeriesStreamingViewModel.IsRunning) || _timer == null)
                {
                    return;
                }

                if (_model?.IsRunning == true)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }
    }
}
