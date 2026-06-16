using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.Views;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Reactive;

namespace Avalonia.Diagnostics
{
    internal static class DevTools
    {
        private static readonly Dictionary<IDevToolsTopLevelGroup, MainWindow> s_open = new();

        public static IDisposable Attach(TopLevel root, KeyGesture gesture)
        {
            return Attach(root, new DevToolsOptions()
            {
                Gesture = gesture,
            });
        }

        public static IDisposable Attach(TopLevel root, DevToolsOptions options)
        {
            void PreviewKeyDown(object? sender, KeyEventArgs e)
            {
                if (options.Gesture.Matches(e))
                {
                    Open(root, options);
                }
            }

            return (root ?? throw new ArgumentNullException(nameof(root))).AddDisposableHandler(
                InputElement.KeyDownEvent,
                PreviewKeyDown,
                RoutingStrategies.Tunnel);
        }

        public static IDisposable Open(TopLevel root, DevToolsOptions options) =>
             Open(new SingleViewTopLevelGroup(root), options, root as Window, null);

        internal static IDisposable Open(IDevToolsTopLevelGroup group, DevToolsOptions options) =>
            Open(group, options, null, null);

        internal static IDisposable Attach(Application application, DevToolsOptions options)
        {
            IDisposable openedDisposable = Disposable.Empty;
            var result = new CompositeDisposable(2);
            result.Add(Disposable.Create(() => openedDisposable.Dispose()));

            // Skip if call on Design Mode
            if (!Design.IsDesignMode)
            {
                var lifeTime = application.ApplicationLifetime
                    as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

                if (lifeTime is null)
                {
                    throw new ArgumentNullException(nameof(application), "DevTools can only attach to applications that support IClassicDesktopStyleApplicationLifetime.");
                }

                if (application.InputManager is not null)
                {
                    result.Add(application.InputManager.PreProcess.Subscribe(e =>
                    {
                        var owner = lifeTime.MainWindow;

                        if (e is RawKeyEventArgs keyEventArgs
                            && keyEventArgs.Type == RawKeyEventType.KeyUp
                            && options.Gesture.Matches(keyEventArgs))
                        {
                            openedDisposable.Dispose();
                            openedDisposable = Open(
                                new ClassicDesktopStyleApplicationLifetimeTopLevelGroup(lifeTime),
                                options,
                                owner,
                                application);
                            e.Handled = true;
                        }
                    }));
                }
            }
            return result;
        }

        private static IDisposable Open(IDevToolsTopLevelGroup topLevelGroup, DevToolsOptions options,
            Window? owner, Application? app)
        {
            var focusedControl = owner?.FocusManager?.GetFocusedElement() as Control;

            // If this request overlaps an already inspected top-level, focus the existing tools window.
            if (s_open.TryGetValue(topLevelGroup, out var mainWindow))
            {
                mainWindow.Activate();
                mainWindow.SelectedControl(focusedControl);
                return Disposable.Empty;
            }

            foreach (var group in s_open)
            {
                if (SharesTopLevel(topLevelGroup, group.Key))
                {
                    group.Value.Activate();
                    group.Value.SelectedControl(focusedControl);
                    return Disposable.Empty;
                }
            }

            AvaloniaObject root = topLevelGroup switch
            {
                ClassicDesktopStyleApplicationLifetimeTopLevelGroup gr => new Controls.Application(gr, app ?? Application.Current!),
                SingleViewTopLevelGroup gr => gr.Items.First(),
                _ => new Controls.TopLevelGroup(topLevelGroup)
            };

            var window = new MainWindow
            {
                Root = root,
                Width = options.Size.Width,
                Height = options.Size.Height,
                Tag = topLevelGroup
            };
            window.SetOptions(options);
            window.SelectedControl(focusedControl);
            window.Closed += DevToolsClosed;
            s_open.Add(topLevelGroup, window);
            if (options.ShowAsChildWindow && owner is not null)
            {
                window.Show(owner);
            }
            else
            {
                window.Show();
            }
            return Disposable.Create(() => window.Close());
        }

        private static void DevToolsClosed(object? sender, EventArgs e)
        {
            var window = (MainWindow)sender!;
            window.Closed -= DevToolsClosed;
            s_open.Remove((IDevToolsTopLevelGroup)window.Tag!);
        }

        internal static int OpenWindowCount => s_open.Count;

        internal static bool SharesTopLevel(IDevToolsTopLevelGroup left, IDevToolsTopLevelGroup right)
        {
            foreach (var leftItem in left.Items)
            {
                foreach (var rightItem in right.Items)
                {
                    if (ReferenceEquals(leftItem, rightItem))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool DoesBelongToDevTool(this Visual v)
        {
            var topLevel = TopLevel.GetTopLevel(v);

            while (topLevel is not null && topLevel is not Views.MainWindow)
            {
                if (topLevel is Avalonia.Controls.Primitives.PopupRoot pr)
                {
                    topLevel = pr.ParentTopLevel;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}
