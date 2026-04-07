using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;

namespace Avalonia.Diagnostics;

/// <summary>
/// Creates standalone DevTools windows that can be hosted by external applications and tools.
/// </summary>
internal static class DevToolsStandaloneWindowFactory
{
    private sealed class EmptyTopLevelGroup : IDevToolsTopLevelGroup
    {
        public static EmptyTopLevelGroup Instance { get; } = new();

        public IReadOnlyList<TopLevel> Items { get; } = Array.Empty<TopLevel>();
    }

    private sealed class StandaloneRemoteRoot : TopLevelGroup, ICloseable
    {
        public StandaloneRemoteRoot(IDevToolsTopLevelGroup group)
            : base(group)
        {
        }

        public event EventHandler? Closed;

        public void NotifyClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Creates a DevTools window bound to the provided diagnostics root.
    /// </summary>
    public static Window CreateWindow(AvaloniaObject root, DevToolsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var launchOptions = options ?? new DevToolsOptions();
        var window = new Views.MainWindow
        {
            Root = root,
            Width = launchOptions.Size.Width,
            Height = launchOptions.Size.Height,
        };
        window.SetOptions(launchOptions);
        return window;
    }

    /// <summary>
    /// Applies DevTools options to a standalone DevTools window created by this factory.
    /// </summary>
    public static void ApplyOptions(Window window, DevToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(options);

        if (window is not Views.MainWindow mainWindow)
        {
            throw new ArgumentException("Window was not created by DevToolsStandaloneWindowFactory.", nameof(window));
        }

        mainWindow.SetOptions(options);
    }

    /// <summary>
    /// Creates a standalone DevTools window configured for external remote endpoint mode.
    /// </summary>
    public static Window CreateRemoteWindow(
        Uri remoteEndpoint,
        DevToolsOptions? options = null,
        Window? diagnosticsRoot = null)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        var launchOptions = options?.Clone() ?? new DevToolsOptions
        {
            ConnectOnStartup = true,
        };
        launchOptions.UseRemoteRuntime = true;
        launchOptions.RemoteRuntimeEndpoint = remoteEndpoint;
        launchOptions.ShowAsChildWindow = false;
        return CreateRemoteWindowCore(launchOptions, diagnosticsRoot);
    }

    /// <summary>
    /// Creates a standalone DevTools window configured for an already connected external remote client session.
    /// Ownership of <paramref name="remoteSession"/> is transferred to the created window.
    /// </summary>
    public static Window CreateRemoteWindow(
        DevToolsRemoteClientSession remoteSession,
        DevToolsOptions? options = null,
        Window? diagnosticsRoot = null,
        RemoteTreeSnapshot? initialCombinedTreeSnapshot = null,
        RemoteSelectionSnapshot? initialCombinedSelectionSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(remoteSession);
        var launchOptions = options?.Clone() ?? new DevToolsOptions
        {
            ConnectOnStartup = true,
        };
        launchOptions.UseRemoteRuntime = true;
        launchOptions.RemoteRuntimeEndpoint = remoteSession.Endpoint;
        launchOptions.ShowAsChildWindow = false;
        return CreateRemoteWindowCore(
            launchOptions,
            diagnosticsRoot,
            viewModel => viewModel.SetPreconnectedRemoteClientSession(
                remoteSession,
                initialCombinedTreeSnapshot,
                initialCombinedSelectionSnapshot));
    }

    private static Window CreateRemoteWindowCore(
        DevToolsOptions launchOptions,
        Window? diagnosticsRoot,
        Action<MainViewModel>? configureViewModel = null)
    {
        var root = CreateStandaloneRoot(diagnosticsRoot);
        var window = new Views.MainWindow
        {
            Root = root,
            Width = launchOptions.Size.Width,
            Height = launchOptions.Size.Height,
        };

        if (window.DataContext is MainViewModel viewModel)
        {
            configureViewModel?.Invoke(viewModel);
        }

        window.SetOptions(launchOptions);
        window.Closed += (_, _) =>
        {
            root.NotifyClosed();

            if (diagnosticsRoot is not null)
            {
                try
                {
                    diagnosticsRoot.Close();
                }
                catch
                {
                    // Best effort shutdown for hidden diagnostics root.
                }
            }
        };

        return window;
    }

    private static StandaloneRemoteRoot CreateStandaloneRoot(Window? diagnosticsRoot)
    {
        return diagnosticsRoot is not null
            ? new StandaloneRemoteRoot(new SingleViewTopLevelGroup(diagnosticsRoot))
            : new StandaloneRemoteRoot(EmptyTopLevelGroup.Instance);
    }
}
