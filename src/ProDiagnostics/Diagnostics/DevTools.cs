using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
                if (MatchesGesture(options.Gesture, e))
                {
                    Open(root, CreateLaunchOptions(options, useRemoteRuntime: false));
                    return;
                }

                if (options.EnableRemoteGesture && MatchesGesture(options.RemoteGesture, e))
                {
                    Open(root, CreateLaunchOptions(options, useRemoteRuntime: true));
                }
            }

            if (options.ConnectOnStartup && (!Design.IsDesignMode || options.AutoConnectFromDesignMode))
            {
                Open(root, CreateLaunchOptions(options, useRemoteRuntime: options.UseRemoteRuntime));
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
            var openedDisposable = new SerialDisposableValue();
            var result = new CompositeDisposable(2);
            result.Add(openedDisposable);

            // Skip design mode unless explicitly enabled.
            if (!Design.IsDesignMode || options.AutoConnectFromDesignMode)
            {
                var lifeTime = application.ApplicationLifetime
                    as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

                if (lifeTime is null)
                {
                    throw new ArgumentNullException(nameof(application), "DevTools can only attach to applications that support IClassicDesktopStyleApplicationLifetime.");
                }

                if (application.InputManager is not null)
                {
                    var startupConnected = false;
                    void TryConnectOnStartup()
                    {
                        if (startupConnected || !options.ConnectOnStartup)
                        {
                            return;
                        }

                        if (lifeTime.MainWindow is { } startupOwner)
                        {
                            openedDisposable.Disposable = Open(
                                new ClassicDesktopStyleApplicationLifetimeTopLevelGroup(lifeTime),
                                options,
                                startupOwner,
                                application);
                            startupConnected = true;
                        }
                    }

                    TryConnectOnStartup();
                    result.Add(application.InputManager.PreProcess.Subscribe(e =>
                    {
                        TryConnectOnStartup();
                        var owner = lifeTime.MainWindow;

                        if (e is RawKeyEventArgs keyEventArgs
                            && keyEventArgs.Type == RawKeyEventType.KeyUp)
                        {
                            if (options.EnableRemoteGesture && options.RemoteGesture.Matches(keyEventArgs))
                            {
                                openedDisposable.Disposable =
                                    Open(
                                        new ClassicDesktopStyleApplicationLifetimeTopLevelGroup(lifeTime),
                                        CreateLaunchOptions(options, useRemoteRuntime: true),
                                        owner,
                                        application);
                                e.Handled = true;
                            }
                            else if (options.Gesture.Matches(keyEventArgs))
                            {
                                openedDisposable.Disposable =
                                    Open(
                                        new ClassicDesktopStyleApplicationLifetimeTopLevelGroup(lifeTime),
                                        CreateLaunchOptions(options, useRemoteRuntime: false),
                                        owner,
                                        application);
                                e.Handled = true;
                            }
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
            AvaloniaObject root = topLevelGroup switch
            {
                ClassicDesktopStyleApplicationLifetimeTopLevelGroup gr => new Controls.Application(gr, app ?? Application.Current!),
                SingleViewTopLevelGroup gr => gr.Items.First(),
                _ => new Controls.TopLevelGroup(topLevelGroup)
            };

            // If single static toplevel is already visible in another devtools window, focus it.
            if (s_open.TryGetValue(topLevelGroup, out var mainWindow))
            {
                mainWindow.Activate();
                mainWindow.SelectedControl(focusedControl);
                return Disposable.Empty;
            }
            if (topLevelGroup.Items.Count == 1 && topLevelGroup.Items is not INotifyCollectionChanged)
            {
                var singleTopLevel = topLevelGroup.Items.First();

                foreach (var group in s_open)
                {
                    if (group.Key.Items.Contains(singleTopLevel))
                    {
                        group.Value.Activate();
                        group.Value.SelectedControl(focusedControl);
                        return Disposable.Empty;
                    }
                }
            }

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

        private static bool MatchesGesture(KeyGesture gesture, KeyEventArgs e)
        {
            if (gesture.Key == Key.None && gesture.KeyModifiers == KeyModifiers.None)
            {
                return false;
            }

            return gesture.Matches(e);
        }

        private static DevToolsOptions CreateLaunchOptions(DevToolsOptions source, bool useRemoteRuntime)
        {
            var launchOptions = source.Clone();
            launchOptions.UseRemoteRuntime = useRemoteRuntime;
            launchOptions.ConnectOnStartup = true;
            return launchOptions;
        }

        internal static bool DoesBelongToDevTool(this Visual v)
        {
            var topLevel = TopLevel.GetTopLevel(v);

            while (topLevel is not null)
            {
                if (topLevel is Views.MainWindow or IDevToolsHostSurface)
                {
                    return true;
                }

                if (topLevel is Avalonia.Controls.Primitives.PopupRoot pr)
                {
                    topLevel = pr.ParentTopLevel;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }
    }
}
