using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Services
{
    internal static class ResourceReferencePickerHost
    {
        private const double PreferredWidth = 920;
        private const double PreferredHeight = 620;
        private const double MinimumWidth = 720;
        private const double MinimumHeight = 480;
        private const double EdgePadding = 12;

        public static async Task<ResourceReferenceCandidate?> ShowAsync(
            Control ownerControl,
            ResourceReferencePickerViewModel viewModel)
        {
            if (TopLevel.GetTopLevel(ownerControl) is Window owner)
            {
                var picker = new ResourceReferencePickerWindow
                {
                    DataContext = viewModel
                };
                return await picker.ShowDialog<ResourceReferenceCandidate?>(owner);
            }

            if (TryShowDockManagedPanel(ownerControl, viewModel, out var dockTask))
            {
                return await dockTask;
            }

            if (TryShowAdornerPanel(ownerControl, viewModel, out var adornerTask))
            {
                return await adornerTask;
            }

            return null;
        }

        private static bool TryShowDockManagedPanel(
            Control ownerControl,
            ResourceReferencePickerViewModel viewModel,
            out Task<ResourceReferenceCandidate?> task)
        {
            task = Task.FromResult<ResourceReferenceCandidate?>(null);

            if (!TryGetDockManagedLayer(ownerControl, out var layer))
            {
                return false;
            }

            var key = $"{typeof(ResourceReferencePickerHost).FullName}.{Guid.NewGuid():N}";
            var completion = new TaskCompletionSource<ResourceReferenceCandidate?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var panel = new ResourceReferencePickerFloatingPanel
            {
                DataContext = viewModel
            };
            var bounds = CreateCenteredBounds(layer.LayerControl.Bounds.Size);

            EventHandler<ResourceReferenceCandidate?>? completed = null;
            EventHandler<Vector>? dragDelta = null;

            completed = (_, candidate) =>
            {
                if (completed != null)
                {
                    panel.Completed -= completed;
                }

                if (dragDelta != null)
                {
                    panel.DragDeltaRequested -= dragDelta;
                }

                layer.HideOverlay(key);
                completion.TrySetResult(candidate);
            };

            dragDelta = (_, delta) =>
            {
                bounds = ClampBounds(
                    new Rect(bounds.X + delta.X, bounds.Y + delta.Y, bounds.Width, bounds.Height),
                    layer.LayerControl.Bounds.Size);
                layer.ShowOverlay(key, panel, bounds);
            };

            panel.Completed += completed;
            panel.DragDeltaRequested += dragDelta;

            try
            {
                layer.ShowOverlay(key, panel, bounds);
            }
            catch
            {
                panel.Completed -= completed;
                panel.DragDeltaRequested -= dragDelta;
                return false;
            }

            task = completion.Task;
            return true;
        }

        private static bool TryShowAdornerPanel(
            Control ownerControl,
            ResourceReferencePickerViewModel viewModel,
            out Task<ResourceReferenceCandidate?> task)
        {
            task = Task.FromResult<ResourceReferenceCandidate?>(null);

            if (AdornerLayer.GetAdornerLayer(ownerControl) is not { } layer)
            {
                return false;
            }

            var adornedElement = TopLevel.GetTopLevel(ownerControl) as Visual ??
                                 ownerControl.GetVisualRoot() as Visual ??
                                 ownerControl;
            var adornedSize = adornedElement is Control control
                ? control.Bounds.Size
                : ownerControl.Bounds.Size;
            var bounds = CreateCenteredBounds(adornedSize);
            var completion = new TaskCompletionSource<ResourceReferenceCandidate?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var panel = new ResourceReferencePickerFloatingPanel
            {
                DataContext = viewModel
            };
            var host = new Canvas
            {
                IsHitTestVisible = true
            };
            AdornerLayer.SetAdornedElement(host, adornedElement);
            Canvas.SetLeft(panel, bounds.X);
            Canvas.SetTop(panel, bounds.Y);
            host.Children.Add(panel);

            EventHandler<ResourceReferenceCandidate?>? completed = null;
            EventHandler<Vector>? dragDelta = null;

            completed = (_, candidate) =>
            {
                if (completed != null)
                {
                    panel.Completed -= completed;
                }

                if (dragDelta != null)
                {
                    panel.DragDeltaRequested -= dragDelta;
                }

                layer.Children.Remove(host);
                completion.TrySetResult(candidate);
            };

            dragDelta = (_, delta) =>
            {
                bounds = ClampBounds(
                    new Rect(bounds.X + delta.X, bounds.Y + delta.Y, bounds.Width, bounds.Height),
                    adornedSize);
                Canvas.SetLeft(panel, bounds.X);
                Canvas.SetTop(panel, bounds.Y);
            };

            panel.Completed += completed;
            panel.DragDeltaRequested += dragDelta;
            layer.Children.Add(host);
            task = completion.Task;
            return true;
        }

        private static Rect CreateCenteredBounds(Size containerSize)
        {
            var width = Math.Min(PreferredWidth, Math.Max(MinimumWidth, containerSize.Width - EdgePadding * 2));
            var height = Math.Min(PreferredHeight, Math.Max(MinimumHeight, containerSize.Height - EdgePadding * 2));
            var x = Math.Max(EdgePadding, (containerSize.Width - width) / 2);
            var y = Math.Max(EdgePadding, (containerSize.Height - height) / 2);
            return new Rect(x, y, width, height);
        }

        private static Rect ClampBounds(Rect bounds, Size containerSize)
        {
            var width = Math.Min(bounds.Width, Math.Max(MinimumWidth, containerSize.Width - EdgePadding * 2));
            var height = Math.Min(bounds.Height, Math.Max(MinimumHeight, containerSize.Height - EdgePadding * 2));
            var maxX = Math.Max(EdgePadding, containerSize.Width - width - EdgePadding);
            var maxY = Math.Max(EdgePadding, containerSize.Height - height - EdgePadding);
            var x = Math.Min(Math.Max(EdgePadding, bounds.X), maxX);
            var y = Math.Min(Math.Max(EdgePadding, bounds.Y), maxY);
            return new Rect(x, y, width, height);
        }

        private static bool TryGetDockManagedLayer(Control ownerControl, out DockManagedLayer layer)
        {
            layer = default;

            var layerType = GetDockManagedWindowLayerType();
            if (layerType is null)
            {
                return false;
            }

            var tryGetLayer = layerType.GetMethod(
                "TryGetLayer",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Visual) },
                modifiers: null);
            var showOverlay = layerType.GetMethod(
                "ShowOverlay",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string), typeof(Control), typeof(Rect), typeof(bool) },
                modifiers: null);
            var hideOverlay = layerType.GetMethod(
                "HideOverlay",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            if (tryGetLayer is null || showOverlay is null || hideOverlay is null)
            {
                return false;
            }

            var layerInstance = tryGetLayer.Invoke(null, new object[] { ownerControl });
            if (layerInstance is not Control layerControl)
            {
                return false;
            }

            layer = new DockManagedLayer(layerInstance, layerControl, showOverlay, hideOverlay);
            return true;
        }

        private static Type? GetDockManagedWindowLayerType()
        {
            try
            {
                return Type.GetType("Dock.Avalonia.Controls.ManagedWindowLayer, Dock.Avalonia") ??
                       AppDomain.CurrentDomain
                           .GetAssemblies()
                           .Select(static assembly => assembly.GetType("Dock.Avalonia.Controls.ManagedWindowLayer", throwOnError: false))
                           .FirstOrDefault(static type => type != null);
            }
            catch
            {
                return null;
            }
        }

        private readonly struct DockManagedLayer
        {
            private readonly object _instance;
            private readonly MethodInfo _showOverlay;
            private readonly MethodInfo _hideOverlay;

            public DockManagedLayer(
                object instance,
                Control layerControl,
                MethodInfo showOverlay,
                MethodInfo hideOverlay)
            {
                _instance = instance;
                LayerControl = layerControl;
                _showOverlay = showOverlay;
                _hideOverlay = hideOverlay;
            }

            public Control LayerControl { get; }

            public void ShowOverlay(string key, Control control, Rect bounds)
            {
                _showOverlay.Invoke(_instance, new object[] { key, control, bounds, true });
            }

            public void HideOverlay(string key)
            {
                _hideOverlay.Invoke(_instance, new object[] { key });
            }
        }
    }
}
